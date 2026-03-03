using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameExternal;
using GameOutside;
using GameOutside.Util;
using Microsoft.AspNetCore.Mvc;

public partial class ActivityController
{
    public record struct SlotMachineDrawReply(int NewDiamond, List<int> Result);
    public record struct RerollSlotMachineRewardReply(int NewDiamond, int RerollTime, int NewReward);
    public record struct TakeMachineDrawRewardReply(int DrawTimeToday, int NewPoint, TakeRewardResult? RewardResult);

    public async Task<(bool, TakeRewardResult?)> TakeExpiredSlotMachineReward(int newActivityId)
    {
        bool databaseChanged = false;
        GeneralReward generalReward = new GeneralReward();
        List<int> totalItemList = new();
        List<int> totalCountList = new();
        return await dbContext.WithRCUDefaultRetry<(bool, TakeRewardResult?)>(async _ =>
        {
            var dataList = await activityService.GetSlotMachineListByPlayerAsync(PlayerId, PlayerShard, TrackingOptions.Tracking);
            foreach (var data in dataList)
            {
                if (data.ActivityId == newActivityId)
                    continue;
                data.DeletedAt = DateTime.UtcNow;
                databaseChanged = true;
                if (data.RewardsInSlot.Count == 0)
                    continue;
                var drawConfigList = serverConfig.GetActivitySlotMachineDrawConfigByActivityId(data.ActivityId);
                if (drawConfigList == null) continue;
                var drawConfig = drawConfigList[data.TodayDrawCount];
                bool hasDoubleUpItem = data.RewardDoubledUpItemCount.Count > data.TodayDrawCount &&
                                       data.RewardDoubledUpItemCount[data.TodayDrawCount] > 0;
                int multiplier = (hasDoubleUpItem ? 2 : 1) * drawConfig.reward_count_multiplier;
                var (itemList, countList, pointAdded) = activityService.ClaimSlotMachineReward(
                    data.ActivityId, multiplier, data.RewardsInSlot);
                if (itemList == null || countList == null) continue;
                totalItemList.AddRange(itemList);
                totalCountList.AddRange(countList);
                data.RewardsInSlot.Clear();
                if (hasDoubleUpItem)
                    data.RewardDoubledUpItemCount[data.TodayDrawCount] -= 1;
            }
            var includeOption = userItemService.CalculateUserAssetIncludeOptions(totalItemList);
            var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return (databaseChanged, null);

            for (int i = 0; i < totalItemList.Count; i++)
                generalReward.AddReward(totalItemList[i], totalCountList[i]);

            var (newCardList, rewardReply) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            return (databaseChanged, rewardReply);
        });
    }

    [HttpPost]
    public async Task<ActionResult<SlotMachineDrawReply>> StartNewSlotMachineDraw(int activityId)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivitySlotMachine, GameVersion);
        if (activity == null || activity.id != activityId)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());
        var activityConfig = serverConfig.GetActivitySlotMachineConfigByActivityId(activityId);
        if (activityConfig == null)
            return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());
        var drawConfig = serverConfig.GetActivitySlotMachineDrawConfigByActivityId(activityId);
        if (drawConfig == null)
            return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());
        var prizePoolConfig = serverConfig.GetActivitySlotMachineDrawRewardConfigByActivityId(activityId);
        if (prizePoolConfig == null)
            return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());

        return await dbContext.WithRCUDefaultRetry<ActionResult<SlotMachineDrawReply>>(async _ =>
        {
            var activityData = await activityService.GetSlotMachineDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (activityData == null)
                activityData = activityService.CreateDefaultSlotMachineData(PlayerId, PlayerShard, activityId);

            var userAsset = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());

            bool newDay = TimeUtils.GetDayDiffBetween(
                TimeUtils.GetCurrentTime(), activityData.LastDrawTime, userAsset.TimeZoneOffset, 0) > 0;
            if (!newDay && activityData.TodayDrawCount >= activityConfig.max_draw_time_per_day)
                return BadRequest(ErrorKind.INVALID_INPUT.Response());
            else if (!activityData.RewardsInSlot.IsNullOrEmpty())
                return BadRequest(ErrorKind.INVALID_INPUT.Response());
            else if (newDay)
                activityData.TodayDrawCount = 0;

            int drawIdx = activityData.TodayDrawCount;
            if (userAsset.DiamondCount < drawConfig[drawIdx].diamond_cost)
                return BadRequest(ErrorKind.DIAMOND_NOT_ENOUGH.Response());

            // 检查都做完了，开始抽奖
            userAsset.DiamondCount -= drawConfig[drawIdx].diamond_cost;
            Random rand = new();
            activityData.LastDrawTime = TimeUtils.GetCurrentTime();
            activityData.RewardsInSlot = new()
            {
                activityService.RandomizeDrawReward(prizePoolConfig, rand),
                activityService.RandomizeDrawReward(prizePoolConfig, rand),
                activityService.RandomizeDrawReward(prizePoolConfig, rand),
            };
            activityData.RerollCounts = new() { 0, 0, 0 };
            activityData.GuaranteeProgressList = new() { 0, 0, 0, 0 };

            // 使用事务确保一致性
            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            SlotMachineDrawReply reply = new() { NewDiamond = userAsset.DiamondCount, Result = activityData.RewardsInSlot };
            return Ok(reply);
        });
    }

    [HttpPost]
    public async Task<ActionResult<TakeRewardResult>> ClaimSlotMachinePointReward(int activityId, int rewardId)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivitySlotMachine, GameVersion);
        if (activity == null || activity.id != activityId)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());
        var activityConfig = serverConfig.GetActivitySlotMachineConfigByActivityId(activityId);
        if (activityConfig == null)
            return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());
        var pointRewardConfig = serverConfig.GetActivitySlotMachinePointRewardConfigByActivityId(activityId);
        if (pointRewardConfig == null)
            return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());
        if (rewardId < 0 || rewardId >= pointRewardConfig.Count)
            return BadRequest(ErrorKind.INVALID_INPUT.Response());

        return await dbContext.WithRCUDefaultRetry<ActionResult<TakeRewardResult>>(async _ =>
        {
            var activityData = await activityService.GetSlotMachineDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (activityData == null)
                activityData = activityService.CreateDefaultSlotMachineData(PlayerId, PlayerShard, activityId);

            var rewardItem = pointRewardConfig[rewardId];
            if (activityData.ActivityPoint < rewardItem.point_required)
                return BadRequest(ErrorKind.INVALID_INPUT.Response());
            if ((activityData.PointRewardClaimStatus & (1 << rewardId)) > 0)
                return BadRequest(ErrorKind.REWARD_CLAIMED.Response());

            // 检查做完了
            GeneralReward reward = new GeneralReward() { ItemList = new(), CountList = new() };
            reward.AddReward(rewardItem.item_id, rewardItem.item_count);
            var includeOption = userItemService.CalculateUserAssetIncludeOptions([rewardItem.item_id]);
            var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            var (newCardList, rewardResult) = await userItemService.TakeReward(userAsset, reward, GameVersion);
            activityData.PointRewardClaimStatus |= (long)(1 << rewardId);

            // 使用事务确保一致性
            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (rewardResult?.AssetsChange != null)
                rewardResult.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            return Ok(rewardResult);
        });
    }

    [HttpPost]
    public async Task<ActionResult<RerollSlotMachineRewardReply>> RerollSlotOfSlotMachine(int activityId, int slotId)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivitySlotMachine, GameVersion);
        if (activity == null || activity.id != activityId)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());
        var activityConfig = serverConfig.GetActivitySlotMachineConfigByActivityId(activityId);
        if (activityConfig == null)
            return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());
        var drawConfigList = serverConfig.GetActivitySlotMachineDrawConfigByActivityId(activityId);
        if (drawConfigList == null)
            return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());

        return await dbContext.WithRCUDefaultRetry<ActionResult<RerollSlotMachineRewardReply>>(async _ =>
        {
            var activityData = await activityService.GetSlotMachineDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (activityData == null)
                activityData = activityService.CreateDefaultSlotMachineData(PlayerId, PlayerShard, activityId);
            if (activityData.RewardsInSlot.IsNullOrEmpty())
                return BadRequest(ErrorKind.INVALID_INPUT.Response());
            if (slotId < 0 || slotId >= activityData.RerollCounts.Count)
                return BadRequest(ErrorKind.INVALID_INPUT.Response());

            var drawConfig = drawConfigList[activityData.TodayDrawCount];
            var prizePoolConfig = serverConfig.GetActivitySlotMachineDrawRewardConfigByActivityId(activityId);
            if (prizePoolConfig == null)
                return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());

            var userAsset = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());

            var rerollTime = activityData.RerollCounts[slotId];
            int rerollCost = (activityConfig.reroll_base_cost + (activityConfig.reroll_base_cost_increment * rerollTime)) *
                             drawConfig.reward_count_multiplier;
            rerollCost = Math.Min(rerollCost, activityConfig.max_reroll_base_cost * drawConfig.reward_count_multiplier);
            if (userAsset.DiamondCount < rerollCost)
                return BadRequest(ErrorKind.INVALID_INPUT.Response());

            // 检查做完了
            userAsset.DiamondCount -= rerollCost;
            int forceQuality = -1;
            for (int i = 3; i >= 0; i--)
            {
                if (activityConfig.guarantee_count_list[i] == 0)
                    continue;
                if (activityData.GuaranteeProgressList[i] >= activityConfig.guarantee_count_list[i])
                {
                    forceQuality = i;
                    break;
                }
            }

            Random rand = new();
            var newReward = activityService.RandomizeDrawReward(prizePoolConfig, rand, forceQuality, activityData.RewardsInSlot[slotId]);
            activityData.RewardsInSlot[slotId] = newReward;
            activityData.RerollCounts[slotId]++;
            var rewardQuality = prizePoolConfig[newReward].quality;
            for (int i = 3; i >= 0; i--)
            {
                if (rewardQuality >= i)
                    activityData.GuaranteeProgressList[i] = 0;
                else
                    activityData.GuaranteeProgressList[i]++;
            }

            // 使用事务确保一致性
            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            var reply = new RerollSlotMachineRewardReply()
            {
                NewDiamond = userAsset.DiamondCount,
                NewReward = newReward,
                RerollTime = activityData.RerollCounts[slotId],
            };
            return Ok(reply);
        });
    }

    [HttpPost]
    public async Task<ActionResult<TakeMachineDrawRewardReply?>> ClaimSlotMachineRewardDirectly(int activityId)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivitySlotMachine, GameVersion);
        if (activity == null || activity.id != activityId)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());
        var drawConfigList = serverConfig.GetActivitySlotMachineDrawConfigByActivityId(activityId);
        if (drawConfigList == null)
            return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());

        return await dbContext.WithRCUDefaultRetry<ActionResult<TakeMachineDrawRewardReply?>>(async _ =>
        {
            var activityData = await activityService.GetSlotMachineDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (activityData == null)
                activityData = activityService.CreateDefaultSlotMachineData(PlayerId, PlayerShard, activityId);
            if (activityData.RewardsInSlot.IsNullOrEmpty())
                return BadRequest(ErrorKind.INVALID_INPUT.Response());

            var drawConfig = drawConfigList[activityData.TodayDrawCount];

            // 检查做完了
            bool hasDoubleUpItem = activityData.RewardDoubledUpItemCount.Count > activityData.TodayDrawCount &&
                                   activityData.RewardDoubledUpItemCount[activityData.TodayDrawCount] > 0;
            int multiplier = (hasDoubleUpItem ? 2 : 1) * drawConfig.reward_count_multiplier;
            var (itemList, countList, pointAdded) = activityService.ClaimSlotMachineReward(activityId, multiplier, activityData.RewardsInSlot);
            if (itemList == null || countList == null)
                return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());
            itemList.Add((int)MoneyType.SlotMachineScore);
            countList.Add(pointAdded);
            GeneralReward generalReward = new GeneralReward() { ItemList = itemList, CountList = countList };
            var includeOption = userItemService.CalculateUserAssetIncludeOptions(itemList);
            var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            var (newCardList, rewardReply) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            if (hasDoubleUpItem)
                activityData.RewardDoubledUpItemCount[activityData.TodayDrawCount] -= 1;
            activityData.TodayDrawCount++;
            activityData.RewardsInSlot.Clear();
            activityData.ActivityPoint += pointAdded;
            if (TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(), activityData.LastDrawTime, userAsset.TimeZoneOffset, 0) > 0)
                activityData.TodayDrawCount = 0;

            // 使用事务确保一致性
            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (rewardReply?.AssetsChange != null)
                rewardReply.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            TakeMachineDrawRewardReply reply = new()
            {
                DrawTimeToday = activityData.TodayDrawCount,
                NewPoint = activityData.ActivityPoint,
                RewardResult = rewardReply
            };
            return Ok(reply);
        });
    }
}