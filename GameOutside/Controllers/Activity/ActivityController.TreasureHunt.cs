using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameExternal;
using GameOutside.Models;
using GameOutside.Util;
using Microsoft.AspNetCore.Mvc;

public partial class ActivityController
{
    public record struct TreasureHuntDrawReply(
        int DrawnSlotIndex,
        int NewDiamond,
        TreasureHuntDataClient TreasureHuntData,
        TakeRewardResult RewardResult);

    public record struct TreasureHuntDrawAllReply(
        List<int> DrawnSlotIndices,
        int NewDiamond,
        TreasureHuntDataClient TreasureHuntData,
        TakeRewardResult RewardResult);

    public record struct TreasureHuntRefreshReply(
        int NewDiamond,
        TreasureHuntDataClient TreasureHuntData);

    /// <summary>
    /// 处理过期的灵犀探宝活动：
    /// 1. 自动发放未领取的积分奖励
    /// 2. 退还未消耗的钥匙（60玉璧/把）
    /// </summary>
    public async Task<(bool, TakeRewardResult?)> HandleExpiredTreasureHuntAsync(UserAssets userAsset)
    {
        bool databaseChanged = false;
        const int diamondPerKey = 60;

        return await dbContext.WithRCUDefaultRetry<(bool, TakeRewardResult?)>(async _ =>
        {
            GeneralReward totalReward = new();
            var dataList = await activityService.GetTreasureHuntListByPlayerAsync(PlayerId, PlayerShard, TrackingOptions.Tracking);
            foreach (var data in dataList)
            {
                var timeConfig = serverConfig.GetActivityConfigById(data.ActivityId);
                // 这里不使用客户端版本，以防有玩家同时运行两个版本
                if (timeConfig == null || activityService.IsOpen(timeConfig, "100.0.0"))
                    continue;
                // 软删除
                data.DeletedAt = DateTime.UtcNow;
                // 1. 自动发放未领取的积分奖励
                var pointRewardConfigs = serverConfig.GetTreasureHuntPointRewardConfigByActivityId(data.ActivityId);
                if (pointRewardConfigs != null)
                {
                    foreach (var rewardConfig in pointRewardConfigs)
                    {
                        // 检查积分是否足够且未领取
                        if (data.ScorePoints >= rewardConfig.point_required &&
                            !data.ScoreRewardClaimStatus.GetNthBits(rewardConfig.reward_id))
                        {
                            // 标记为已领取
                            data.ScoreRewardClaimStatus = data.ScoreRewardClaimStatus.SetNthBits(rewardConfig.reward_id, true);
                            // 添加奖励
                            totalReward.AddReward(rewardConfig.item_id, rewardConfig.item_count);
                            databaseChanged = true;
                        }
                    }
                }

                // 2. 退还未消耗的钥匙
                if (data.KeyCount > 0)
                {
                    int diamondRefund = data.KeyCount * diamondPerKey;
                    totalReward.AddReward((int)MoneyType.Diamond, diamondRefund);
                    data.KeyCount = 0;
                    databaseChanged = true;
                }
            }

            if (totalReward.ItemList.Count == 0)
                return (databaseChanged, null);

            // 发放所有奖励
            var (_, rewardResult) = await userItemService.TakeReward(userAsset, totalReward, GameVersion);
            return (databaseChanged, rewardResult);
        });
    }

    /// <summary>
    /// 抽取一个灵犀探宝格子（随机未开启的格子，只能使用玉璧）
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TreasureHuntDrawReply>> DrawTreasureHuntReward(int activityId)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivityTreasureHunt, GameVersion);
        if (activity == null || activity.id != activityId)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());
        var config = serverConfig.GetTreasureHuntConfigByActivityId(activityId);
        if (config == null)
            return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());

        return await dbContext.WithRCUDefaultRetry<ActionResult<TreasureHuntDrawReply>>(async _ =>
        {
            var activityData = await activityService.GetTreasureHuntDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (activityData == null)
                activityData = activityService.CreateDefaultTreasureHuntData(PlayerId, PlayerShard, activityId);

            var userAsset = await userAssetService.GetUserAssetsDetailedAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());

            var currentTime = TimeUtils.GetCurrentTime();
            // 跨天刷新奖池和刷新次数
            bool newDay = TimeUtils.GetDayDiffBetween(currentTime, activityData.LastRefreshTime, userAsset.TimeZoneOffset, 0) > 0;
            if (newDay || activityData.RewardSlots.Count == 0)
            {
                activityData.RewardSlots = activityService.GenerateTreasureHuntPool(activityId);
                dbContext.Entry(activityData).Property(t => t.RewardSlots).IsModified = true;
                activityData.TodayRefreshCount = 0;
                activityData.LastRefreshTime = currentTime;
            }

            // 计算已开启格子数
            int openedCount = activityData.RewardSlots.Count(s => s.HasOpen);
            int drawCost = activityService.CalculateTreasureHuntDrawCost(activityId, openedCount);

            // 扣除玉璧消耗
            if (userAsset.DiamondCount < drawCost)
                return BadRequest(ErrorKind.DIAMOND_NOT_ENOUGH.Response());
            userAsset.DiamondCount -= drawCost;

            // 随机选择一个未开启的格子
            int? slotIndex = activityService.DrawRandomTreasureHuntSlot(activityData.RewardSlots);
            if (slotIndex == null)
                return BadRequest(ErrorKind.INVALID_INPUT.Response());

            var slot = activityData.RewardSlots[slotIndex.Value];
            slot.HasOpen = true;
            dbContext.Entry(activityData).Property(t => t.RewardSlots).IsModified = true;

            // 获取奖励物品
            var rewardInfo = activityService.GetTreasureHuntRewardById(activityId, slot.Id);
            if (rewardInfo == null)
                return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());

            var (itemId, itemCount, point) = rewardInfo.Value;
            activityData.ScorePoints += point;

            // 发放奖励
            GeneralReward reward = new GeneralReward();
            reward.AddReward(itemId, itemCount);
            var (newCardList, rewardResult) = await userItemService.TakeReward(userAsset, reward, GameVersion,
                activityTreasureHunt: activityData);
            if (rewardResult == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());
            // 奖励展示保护积分奖励
            reward.AddReward((int)MoneyType.TreasureHuntScore, point);

            // 使用事务确保一致性
            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            rewardResult.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            return Ok(new TreasureHuntDrawReply
            {
                DrawnSlotIndex = slotIndex.Value,
                NewDiamond = userAsset.DiamondCount,
                TreasureHuntData = activityData.ToClientApi(),
                RewardResult = rewardResult
            });
        });
    }

    /// <summary>
    /// 一键抽取所有未开启的格子（只能使用钥匙）
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TreasureHuntDrawAllReply>> DrawAllTreasureHuntRewards(int activityId)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivityTreasureHunt, GameVersion);
        if (activity == null || activity.id != activityId)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());
        var config = serverConfig.GetTreasureHuntConfigByActivityId(activityId);
        if (config == null)
            return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());

        return await dbContext.WithRCUDefaultRetry<ActionResult<TreasureHuntDrawAllReply>>(async _ =>
        {
            var activityData = await activityService.GetTreasureHuntDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (activityData == null)
                activityData = activityService.CreateDefaultTreasureHuntData(PlayerId, PlayerShard, activityId);

            var userAsset = await userAssetService.GetUserAssetsDetailedAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());

            var currentTime = TimeUtils.GetCurrentTime();
            // 跨天刷新奖池和刷新次数
            bool newDay = TimeUtils.GetDayDiffBetween(currentTime, activityData.LastRefreshTime, userAsset.TimeZoneOffset, 0) > 0;
            if (newDay || activityData.RewardSlots.Count == 0)
            {
                activityData.RewardSlots = activityService.GenerateTreasureHuntPool(activityId);
                dbContext.Entry(activityData).Property(t => t.RewardSlots).IsModified = true;
                activityData.TodayRefreshCount = 0;
                activityData.LastRefreshTime = currentTime;
            }

            // 找到所有未开启的格子
            var unopenedIndices = activityData.RewardSlots
                .Select((slot, index) => new { slot, index })
                .Where(x => !x.slot.HasOpen)
                .Select(x => x.index)
                .ToList();

            if (unopenedIndices.Count == 0)
                return BadRequest(ErrorKind.INVALID_INPUT.Response());

            // 检查钥匙数量是否足够
            int keysRequired = unopenedIndices.Count;
            if (activityData.KeyCount < keysRequired)
                return BadRequest(ErrorKind.INSUFFICIENT_KEY.Response());

            // 扣除钥匙消耗
            activityData.KeyCount -= keysRequired;

            // 收集所有奖励
            GeneralReward totalReward = new GeneralReward();
            List<int> drawnIndices = new();
            int totalPointsAdded = 0;

            foreach (var slotIndex in unopenedIndices)
            {
                var slot = activityData.RewardSlots[slotIndex];
                slot.HasOpen = true;
                drawnIndices.Add(slotIndex);

                var rewardInfo = activityService.GetTreasureHuntRewardById(activityId, slot.Id);
                if (rewardInfo == null)
                    continue;

                var (itemId, itemCount, point) = rewardInfo.Value;
                totalPointsAdded += point;
                totalReward.AddReward(itemId, itemCount);
            }

            dbContext.Entry(activityData).Property(t => t.RewardSlots).IsModified = true;
            activityData.ScorePoints += totalPointsAdded;
            // 发放奖励
            var (newCardList, rewardResult) = await userItemService.TakeReward(userAsset, totalReward, GameVersion,
                activityTreasureHunt: activityData);
            if (rewardResult == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

            // 奖励展示保护积分奖励
            totalReward.AddReward((int)MoneyType.TreasureHuntScore, totalPointsAdded);

            // 使用事务确保一致性
            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (rewardResult?.AssetsChange != null)
                rewardResult.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            return Ok(new TreasureHuntDrawAllReply
            {
                DrawnSlotIndices = drawnIndices,
                NewDiamond = userAsset.DiamondCount,
                TreasureHuntData = activityData.ToClientApi(),
                RewardResult = rewardResult,
            });
        });
    }


    public enum RefreshTreasureHuntPoolType
    {
        Diamond,
        Key
    }

    /// <summary>
    /// 刷新灵犀探宝奖池
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TreasureHuntRefreshReply>> RefreshTreasureHuntPool(
        int activityId,
        RefreshTreasureHuntPoolType refreshType)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivityTreasureHunt, GameVersion);
        if (activity == null || activity.id != activityId)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());
        var config = serverConfig.GetTreasureHuntConfigByActivityId(activityId);
        if (config == null)
            return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());

        return await dbContext.WithRCUDefaultRetry<ActionResult<TreasureHuntRefreshReply>>(async _ =>
        {
            var activityData = await activityService.GetTreasureHuntDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (activityData == null)
                activityData = activityService.CreateDefaultTreasureHuntData(PlayerId, PlayerShard, activityId);

            var userAsset = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());

            var currentTime = TimeUtils.GetCurrentTime();
            // 跨天刷新次数
            bool newDay = TimeUtils.GetDayDiffBetween(currentTime, activityData.LastRefreshTime, userAsset.TimeZoneOffset, 0) > 0;
            if (newDay)
            {
                activityData.TodayRefreshCount = 0;
            }

            // 检查刷新次数限制
            if (activityData.TodayRefreshCount >= config.daily_max_refresh_time)
                return BadRequest(ErrorKind.EXCEED_DAILY_LIMIT.Response());

            // 检查临近0点时手动刷新限制
            var now = TimeUtils.GetCurrentTime();
            var nextMidnight = TimeUtils.GetLocalMidnightEpoch(userAsset.TimeZoneOffset, 0) + 86400;
            if (nextMidnight - now < config.refresh_lock_seconds)
                return BadRequest(ErrorKind.REFRESH_COOLDOWN.Response());

            switch (refreshType)
            {
                case RefreshTreasureHuntPoolType.Diamond:
                    // 计算刷新消耗
                    int refreshCost
                        = activityService.CalculateTreasureHuntRefreshCost(activityId, activityData.TodayRefreshCount);
                    if (userAsset.DiamondCount < refreshCost)
                        return BadRequest(ErrorKind.DIAMOND_NOT_ENOUGH.Response());
                    // 扣除消耗
                    userAsset.DiamondCount -= refreshCost;
                    break;
                case RefreshTreasureHuntPoolType.Key:
                    if (activityData.KeyCount < 1)
                        return BadRequest(ErrorKind.INSUFFICIENT_KEY.Response());
                    // 扣除消耗
                    activityData.KeyCount -= 1;
                    break;
            }

            // 生成新奖池
            activityData.RewardSlots = activityService.GenerateTreasureHuntPool(activityId);
            dbContext.Entry(activityData).Property(t => t.RewardSlots).IsModified = true;
            activityData.TodayRefreshCount++;
            activityData.LastRefreshTime = currentTime;

            // 使用事务确保一致性
            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            return Ok(new TreasureHuntRefreshReply
            {
                NewDiamond = userAsset.DiamondCount,
                TreasureHuntData = activityData.ToClientApi()
            });
        });
    }

    /// <summary>
    /// 领取灵犀探宝积分奖励
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TakeRewardResult>> ClaimTreasureHuntScoreReward(int activityId, int rewardId)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivityTreasureHunt, GameVersion);
        if (activity == null || activity.id != activityId)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());
        var pointRewardConfig = serverConfig.GetTreasureHuntPointRewardConfigByActivityId(activityId);
        if (pointRewardConfig == null)
            return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());

        var rewardConfigItem = pointRewardConfig.FirstOrDefault(c => c.reward_id == rewardId);
        if (rewardConfigItem == null)
            return BadRequest(ErrorKind.INVALID_INPUT.Response());

        return await dbContext.WithRCUDefaultRetry<ActionResult<TakeRewardResult>>(async _ =>
        {
            var activityData = await activityService.GetTreasureHuntDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (activityData == null)
                activityData = activityService.CreateDefaultTreasureHuntData(PlayerId, PlayerShard, activityId);

            // 检查积分是否足够
            if (activityData.ScorePoints < rewardConfigItem.point_required)
                return BadRequest(ErrorKind.INVALID_INPUT.Response());

            // 检查是否已领取（位存储）
            if (activityData.ScoreRewardClaimStatus.GetNthBits(rewardId))
                return BadRequest(ErrorKind.REWARD_CLAIMED.Response());

            // 发放奖励
            GeneralReward reward = new GeneralReward();
            reward.AddReward(rewardConfigItem.item_id, rewardConfigItem.item_count);
            var includeOption = userItemService.CalculateUserAssetIncludeOptions(new List<int> { rewardConfigItem.item_id });
            var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());

            var (newCardList, rewardResult) = await userItemService.TakeReward(userAsset, reward, GameVersion,
                activityTreasureHunt: activityData);
            activityData.ScoreRewardClaimStatus = activityData.ScoreRewardClaimStatus.SetNthBits(rewardId, true);

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
    public async Task<ActionResult<TreasureHuntDataClient?>> GetTreasureHuntData()
    {
        return await dbContext.WithRCUDefaultRetry<ActionResult<TreasureHuntDataClient?>>(async _ =>
        {
            var openingActivity
                = activityService.GetOpeningActivityByType(ActivityType.ActivityTreasureHunt, GameVersion);
            var userAsset = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET);
            var (databaseChanged, result) = await GetTreasureHuntDataAsync(openingActivity, userAsset);
            if (databaseChanged)
            {
                await dbContext.SaveChangesWithDefaultRetryAsync();
            }

            return result;
        });
    }

    private async Task<(bool databaseChanged, TreasureHuntDataClient? result)> GetTreasureHuntDataAsync(
        ActivityTimeConfig? timeConfig,
        UserAssets userAsset)
    {
        if (timeConfig == null)
            return (false, null);
        bool databaseChanged = false;
        var treasureHuntData
            = await activityService.GetTreasureHuntDataAsync(PlayerId, PlayerShard, timeConfig.id,
                TrackingOptions.Tracking);
        if (treasureHuntData == null)
        {
            treasureHuntData = activityService.CreateDefaultTreasureHuntData(PlayerId, PlayerShard, timeConfig.id);
            databaseChanged = true;
        }

        var currentTime = TimeUtils.GetCurrentTime();
        // 跨天刷新奖池和刷新次数
        bool newDay = TimeUtils.GetDayDiffBetween(currentTime, treasureHuntData.LastRefreshTime,
            userAsset.TimeZoneOffset, 0) > 0;
        if (newDay || treasureHuntData.RewardSlots.Count == 0)
        {
            treasureHuntData.RewardSlots = activityService.GenerateTreasureHuntPool(timeConfig.id);
            dbContext.Entry(treasureHuntData).Property(t => t.RewardSlots).IsModified = true;
            treasureHuntData.TodayRefreshCount = 0;
            treasureHuntData.LastRefreshTime = currentTime;
            databaseChanged = true;
        }

        return (databaseChanged, treasureHuntData.ToClientApi());
    }
}
