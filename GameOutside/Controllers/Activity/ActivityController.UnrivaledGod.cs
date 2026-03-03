using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameExternal;
using GameOutside;
using GameOutside.Models;
using GameOutside.Util;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;

public partial class ActivityController
{
    public record struct DrawUnrivaledGodRewardReply(UnrivaledGodDataClient UnrivaledGodData, TakeRewardResult Reward);

    [HttpPost]
    public async Task<ActionResult<DrawUnrivaledGodRewardReply>> DrawUnrivaledGodReward(int activityId, int drawCount)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivityUnrivaledGod, GameVersion);
        if (activity == null || activity.id != activityId)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());

        return await dbContext.WithRCUDefaultRetry<ActionResult<DrawUnrivaledGodRewardReply>>(async _ =>
        {
            var activityData = await activityService.GetUnrivaledGodDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (activityData == null)
                return BadRequest(ErrorKind.NO_ACTIVITY_DATA.Response());

            // 无双神将抽奖需要先仓检再随奖励，这里必须获取完整的资产信息
            var userAsset = await userAssetService.GetUserAssetsDetailedAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());

            var unrivaledGodConfig = serverConfig.GetUnrivaledGodConfigByActivityId(activityId);
            if (unrivaledGodConfig == null)
                return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());

            int costKeyCount = Math.Min(activityData.KeyCount, drawCount);
            int costDiamond = unrivaledGodConfig.cost_diamond * (drawCount - costKeyCount);

            if (costDiamond > 0)
            {
                if (costDiamond > userAsset.DiamondCount)
                {
                    return BadRequest(ErrorKind.DIAMOND_NOT_ENOUGH.Response());
                }

                // 扣除掉玉璧
                userAsset.DiamondCount -= costDiamond;
            }

            // 扣掉钥匙
            activityData.KeyCount -= costKeyCount;

            // 开始抽奖流程
            var rewardPool = FilterRewardPool(unrivaledGodConfig, userAsset);
            var assetChange = new UserAssetsChange();
            var finalReward = new GeneralReward() { ItemList = [], CountList = [] };
            var newCardList = new List<UserCard>();

            for (int i = 0; i < drawCount; i++)
            {
                activityData.GuaranteeProgress++;
                var randomReward = rewardPool.WeightedRandomSelectOne(item => item.Weight)!;
                var rewardId = randomReward.ItemId;
                // 重置一下保底
                if (rewardId == unrivaledGodConfig.unrivaled_card_id)
                    activityData.GuaranteeProgress = 0;

                // 替换为保底
                if (activityData.GuaranteeProgress >= unrivaledGodConfig.guarantee_count &&
                    CanGetItem(userAsset, unrivaledGodConfig.unrivaled_card_id))
                {
                    rewardId = unrivaledGodConfig.unrivaled_card_id;
                    // 重置保底
                    activityData.GuaranteeProgress = 0;
                }

                // 发奖
                int rewardCount = 1;
                // 需要对积分随机一下范围
                if (rewardId == (int)MoneyType.UnrivaledScore)
                {
                    rewardCount = Random.Shared.Next(unrivaledGodConfig.score_min, unrivaledGodConfig.score_max + 1);
                }

                List<int> itemList = [rewardId];
                List<int> countList = [rewardCount];
                var result = await userItemService.UnpackItemList(userAsset, itemList, countList, GameVersion,
                assetChange, activityUnrivaledGod: activityData);
                if (result == null)
                    return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());
                newCardList.AddRange(result.NewCardList);
                // 检测下需不需要刷新奖池
                if (!CanGetItem(userAsset, rewardId))
                {
                    rewardPool.RemoveAll(item => item.ItemId == rewardId);
                }

                finalReward.ItemList.AddRange(itemList);
                finalReward.CountList.AddRange(countList);
            }

            assetChange.FillAssetInfo(userAsset);
            // 抽奖累计应该记录到任务里
            activityService.RecordUnrivaledGodTask(activityData, ActivityTaskKeys.AccuUnrivaledGodDrawReward, drawCount,
                userAsset.TimeZoneOffset);

            // 使用事务确保一致性
            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            assetChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            return Ok(new DrawUnrivaledGodRewardReply
            {
                UnrivaledGodData = activityData.ToClientApi(),
                Reward = new TakeRewardResult() { AssetsChange = assetChange, Reward = finalReward }
            });
        });

    }

    private List<RewardPoolItem> FilterRewardPool(
        UnrivaledGodConfig unrivaledGodConfig,
        UserAssets userAsset)
    {
        var rewardPool = new List<RewardPoolItem>();
        for (int i = 0; i < unrivaledGodConfig.item_list.Length; i++)
        {
            int itemId = unrivaledGodConfig.item_list[i];
            if (!CanGetItem(userAsset, itemId))
                continue;

            rewardPool.Add(new RewardPoolItem() { ItemId = itemId, Weight = unrivaledGodConfig.weight_list[i] });
        }

        return rewardPool;
    }

    private bool CanGetItem(UserAssets userAssets, int itemId)
    {
        var itemConfig = serverConfig.GetItemConfigById(itemId);
        if (itemConfig == null)
            return false;
        switch (itemConfig.type)
        {
            case ItemType.AvatarFrame:
            case ItemType.Avatar:
            case ItemType.NameCard:
            case ItemType.IdleRewardBox:
                var itemData = userAssets.UserItems.FirstOrDefault(item => item.ItemId == itemId);
                if (itemData != null)
                    return false;
                break;
            case ItemType.Hero:
                var heroConfig = serverConfig.GetHeroConfigByKey(itemConfig.detailed_key);
                if (heroConfig == null)
                    return false;
                if (userAssets.Heroes.Contains(heroConfig.id))
                    return false;
                break;
            default:
                return true;
        }

        return true;
    }


    public record struct ClaimUnrivaledGodTaskRewardReply(
        Dictionary<string, UnrivaledGodTask> TaskRecord,
        TakeRewardResult RewardResult);

    [HttpPost]
    public async Task<ActionResult<ClaimUnrivaledGodTaskRewardReply>> ClaimUnrivaledGodTaskReward(
        int activityId, string taskKey)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivityUnrivaledGod, GameVersion);
        if (activity == null || activity.id != activityId)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());

        var taskConfig = serverConfig.GetUnrivaledGodTaskConfig(activityId, taskKey);
        if (taskConfig == null)
            return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());

        return await dbContext.WithRCUDefaultRetry<ActionResult<ClaimUnrivaledGodTaskRewardReply>>(async _ =>
        {
            var activityData = await activityService.GetUnrivaledGodDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (activityData == null)
                return BadRequest(ErrorKind.NO_ACTIVITY_DATA.Response());

            if (!activityData.TaskRecord.TryGetValue(taskKey, out var record))
            {
                record = new UnrivaledGodTask()
                {
                    Progress = 0,
                    Claimed = false,
                    UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                };
                activityData.TaskRecord.Add(taskKey, record);
            }

            // 提前计算好需要Include的内容
            var includeOption = userItemService.CalculateUserAssetIncludeOptions([taskConfig.reward_item]);
            var userAsset
                = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            // 判断是不是跨天了
            var currentTime = TimeUtils.GetCurrentTime();
            if (taskConfig.is_daily &&
                TimeUtils.GetDayDiffBetween(currentTime, record.UpdatedAt, userAsset.TimeZoneOffset, 0) > 0)
            {
                record.Progress = 0;
                record.Claimed = false;
            }

            // 判断可不可以领奖
            if (record.Claimed)
                return BadRequest(ErrorKind.REWARD_CLAIMED.Response());
            if (record.Progress < taskConfig.target_progress)
                return BadRequest(ErrorKind.UNRIVALED_TASK_NOT_FINISHED.Response());

            record.Claimed = true;
            // 标记为Modify
            dbContext.Entry(activityData).Property(t => t.TaskRecord).IsModified = true;

            // 发奖
            var generalReward
                = new GeneralReward() { ItemList = [taskConfig.reward_item], CountList = [taskConfig.reward_count] };
            var (newCardList, takeRewardResult) = await userItemService.TakeReward(userAsset, generalReward, GameVersion, activityData);
            if (takeRewardResult == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

            // 使用事务确保一致性
            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (takeRewardResult.AssetsChange is not null)
                takeRewardResult.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            return Ok(new ClaimUnrivaledGodTaskRewardReply()
            {
                TaskRecord = activityData.TaskRecord,
                RewardResult = takeRewardResult,
            });
        });
    }

    public record struct ExchangeUnrivaledGodReply(int ScorePoint, int Count, TakeRewardResult RewardResult);

    [HttpPost]
    public async Task<ActionResult<ExchangeUnrivaledGodReply>> UnrivaledGodExchange(int activityId, int exchangeId)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivityUnrivaledGod, GameVersion);
        if (activity == null || activity.id != activityId)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());

        var exchangeConfig = serverConfig.GetUnrivaledGodExchangeConfig(activityId, exchangeId);
        if (exchangeConfig == null)
            return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());

        return await dbContext.WithRCUDefaultRetry<ActionResult<ExchangeUnrivaledGodReply>>(async _ =>
        {
            var activityData = await activityService.GetUnrivaledGodDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (activityData == null)
                return BadRequest(ErrorKind.NO_ACTIVITY_DATA.Response());

            // 提前计算好需要Include的内容
            var includeOption = userItemService.CalculateUserAssetIncludeOptions([exchangeConfig.item_id]);
            var userAsset
                = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            // 检查仓储上限
            if (!CanGetItem(userAsset, exchangeConfig.item_id))
                return BadRequest(ErrorKind.ITEM_COUNT_REACH_MAXIMUM.Response());

            if (!activityData.ExchangeRecord.TryGetValue(exchangeId, out var exchangeCount))
            {
                exchangeCount = 0;
                activityData.ExchangeRecord.Add(exchangeId, exchangeCount);
            }

            // 检查兑换上限
            if (exchangeConfig.limit_count > 0 && exchangeCount >= exchangeConfig.limit_count)
                return BadRequest(ErrorKind.UNRIVALED_GOD_EXCHANGE_COUNT_LIMIT.Response());
            // 检查积分
            if (exchangeConfig.price > activityData.ScorePoint)
                return BadRequest(ErrorKind.UNRIVALED_SCORE_NOT_ENOUGH.Response());

            exchangeCount++;
            activityData.ExchangeRecord[exchangeId] = exchangeCount;
            // 标记为Modify
            dbContext.Entry(activityData).Property(t => t.ExchangeRecord).IsModified = true;
            // 扣除积分
            activityData.ScorePoint -= exchangeConfig.price;

            // 发放物品
            var generalReward
                = new GeneralReward() { ItemList = [exchangeConfig.item_id], CountList = [exchangeConfig.item_count] };
            var (newCardList, takeRewardResult) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            if (takeRewardResult == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

            // 使用事务确保一致性
            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (takeRewardResult.AssetsChange is not null)
                takeRewardResult.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            return Ok(new ExchangeUnrivaledGodReply()
            {
                ScorePoint = activityData.ScorePoint,
                Count = exchangeCount,
                RewardResult = takeRewardResult
            });
        });
    }
}