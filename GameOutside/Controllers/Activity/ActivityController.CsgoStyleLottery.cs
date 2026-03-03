using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameExternal;
using GameOutside;
using GameOutside.Models;
using GameOutside.Util;
using Microsoft.AspNetCore.Mvc;

public partial class ActivityController
{
    private const int CsgoLotteryRecordMax = 200;
    private const int CsgoLotteryTenDrawCount = 10;

    public record struct CsgoLotteryDrawReply(CsgoStyleLotteryDataClient LotteryData, TakeRewardResult RewardResult);
    public record struct FetchCsgoLotteryDataReply(
        CsgoStyleLotteryDataClient? LotteryData,
        TakeRewardResult? PassDailyRewardResult,
        TakeRewardResult? ExpiredRewardResult);

    /// <summary>
    /// Csgo风格抽奖 - 抽奖接口
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CsgoLotteryDrawReply>> DrawCsgoStyleLottery(bool useDiamond)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivityCsgoStyleLottery, GameVersion);
        if (activity == null)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());
        var activityId = activity.id;

        var lotteryConfigList = serverConfig.GetCsgoLotteryConfigListByActivityId(activityId);
        if (lotteryConfigList == null || lotteryConfigList.Count == 0)
            return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());

        var guaranteeList = serverConfig.GetParameterIntList(Params.CsgoLotteryGuaranteeList);
        if (!serverConfig.TryGetParameterInt(Params.CsgoLotteryDrawBaseDiamondCost, out var baseCost) ||
            !serverConfig.TryGetParameterInt(Params.CsgoLotteryDrawDiamondCostGrowth, out var costGrow) ||
            !serverConfig.TryGetParameterInt(Params.CsgoLotteryDrawMaxDiamondCost, out var maxCost) ||
            !serverConfig.TryGetParameterInt(Params.CsgoLotteryDrawLimitPerDay, out var drawLimitPerDay) ||
            guaranteeList == null)
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());

        return await dbContext.WithRCUDefaultRetry<ActionResult<CsgoLotteryDrawReply>>(async _ =>
        {
            var lotteryData = await activityService.GetCsgoStyleLotteryDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (lotteryData == null)
                lotteryData = activityService.CreateDefaultCsgoStyleLotteryData(PlayerId, PlayerShard, activityId);
            
            var userAsset = await userAssetService.GetUserAssetsDetailedAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            
            // 计算并扣除消耗：根据玩家选项计算扣除数量
            // 因为要计算include option，所以得等到计算出奖励之后扣
            var currentTime = TimeUtils.GetCurrentTime();
            if (TimeUtils.GetDayDiffBetween(currentTime, lotteryData.LotteryDrawInfoRefreshTimestamp, userAsset.TimeZoneOffset, 0) > 0)
            {
                lotteryData.KeyPurchaseCountByDiamond = 0;
                lotteryData.TotalLotteryDrawToday = 0;
                lotteryData.LotteryDrawInfoRefreshTimestamp = currentTime;
            }
            
            if (lotteryData.TotalLotteryDrawToday >= drawLimitPerDay)
                return BadRequest(ErrorKind.CSGO_LOTTERY_RUN_OUT_TODAY.Response());
            
            if (useDiamond)
            {
                int diamondCostThisDraw = Math.Min(maxCost, baseCost + (lotteryData.KeyPurchaseCountByDiamond * costGrow));
                if (userAsset.DiamondCount < diamondCostThisDraw)
                    return BadRequest(ErrorKind.DIAMOND_NOT_ENOUGH.Response());
                
                lotteryData.KeyPurchaseCountByDiamond += 1;
                userAsset.DiamondCount -= diamondCostThisDraw;
            }
            else
            {
                if (lotteryData.KeyCount <= 0)
                    return BadRequest(ErrorKind.INSUFFICIENT_CSGO_LOTTERY.Response());
                lotteryData.KeyCount -= 1;
            }
            
            // 开始抽奖流程
            var finalReward = new GeneralReward() { ItemList = [], CountList = [] };

            // 保底，计算距离目前举例上次抽到对应品质奖品的抽奖次数，然后根据举例计算保底等级
            int? forceQuality = null;
            List<int> distanceToLastQualifiedReward = new List<int>(Enumerable.Repeat(0, guaranteeList.Count ));
            int qualityCheckStart = 0;
            for (int i = lotteryData.RewardRecord.Count - 1; i >= 0; i--)
            {
                var pastRewardId = lotteryData.RewardRecord[i];
                var pastRewardConfig = serverConfig.GetCsgoLotteryConfigByActivityIdAndRewardId(activityId, pastRewardId);
                if (pastRewardConfig == null)
                    return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());
                if (pastRewardConfig.quality >= guaranteeList.Count - 1)
                    break;
                qualityCheckStart = Math.Max(qualityCheckStart, pastRewardConfig.quality);
                for (int quality = qualityCheckStart; quality < guaranteeList.Count; quality++)
                {
                    if (pastRewardConfig.quality < quality)
                        distanceToLastQualifiedReward[quality] += 1;
                }
            }
            for (int i = guaranteeList.Count - 1; i >= 0; i--)
            {
                // "每X次必出" => 连续未出达到 X-1 时，下一抽强制该品质
                if (guaranteeList[i] > 0 && distanceToLastQualifiedReward[i] >= guaranteeList[i] - 1)
                {
                    forceQuality = i;
                    break;
                }
            }

            var prizePool = FilterRewardPool(lotteryData.RewardRecord.Count,
                lotteryConfigList, forceQuality);
            var randomlySelected = prizePool.WeightedRandomSelectOne(item => item.Weight)!;
            var rewardId = randomlySelected.ItemId;
            var rewardConfig = serverConfig.GetCsgoLotteryConfigByActivityIdAndRewardId(activityId, rewardId);
            if (rewardConfig == null)
                return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());

            // 计算奖励内容
            List<int> itemList = rewardConfig.item_list.ToList();
            List<int> countList = rewardConfig.count_list.ToList();

            // 增加活动积分
            itemList.Add((int)MoneyType.CsgoLotteryPoint);
            countList.Add(rewardConfig.point_reward);
            
            lotteryData.TotalLotteryDrawToday += 1;

            // 记录抽奖结果
            lotteryData.RewardRecord.Add(rewardId);
            lotteryData.RewardRecordTime.Add(currentTime);
            TrimCsgoLotteryRewardRecord(lotteryData);
            
            // 发放奖励
            finalReward.ItemList.AddRange(itemList);
            finalReward.CountList.AddRange(countList);
            
            var (newCardList, takeRewardResult) = await userItemService.TakeReward(userAsset, finalReward, GameVersion);
            if (takeRewardResult == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

            // 记录抽奖任务进度
            activityService.RecordCsgoStyleLotteryTask(lotteryData, ActivityTaskKeys.DrawCsgoLottery, 1, userAsset.TimeZoneOffset);

            // 使用事务确保一致性
            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            takeRewardResult.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            return Ok(new CsgoLotteryDrawReply
            {
                LotteryData = lotteryData.ToClientApi(userAsset),
                RewardResult = takeRewardResult
            });
        });
    }

    /// <summary>
    /// Csgo风格抽奖 - 十连抽接口
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CsgoLotteryDrawReply>> DrawCsgoStyleLotteryTenTime()
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivityCsgoStyleLottery, GameVersion);
        if (activity == null)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());
        var activityId = activity.id;

        var lotteryConfigList = serverConfig.GetCsgoLotteryConfigListByActivityId(activityId);
        if (lotteryConfigList == null || lotteryConfigList.Count == 0)
            return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());

        var guaranteeList = serverConfig.GetParameterIntList(Params.CsgoLotteryGuaranteeList);
        if (!serverConfig.TryGetParameterInt(Params.CsgoLotteryDrawBaseDiamondCost, out var baseCost) ||
            !serverConfig.TryGetParameterInt(Params.CsgoLotteryDrawDiamondCostGrowth, out var costGrow) ||
            !serverConfig.TryGetParameterInt(Params.CsgoLotteryDrawMaxDiamondCost, out var maxCost) ||
            !serverConfig.TryGetParameterInt(Params.CsgoLotteryDrawLimitPerDay, out var drawLimitPerDay) ||
            guaranteeList == null)
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());

        return await dbContext.WithRCUDefaultRetry<ActionResult<CsgoLotteryDrawReply>>(async _ =>
        {
            var lotteryData = await activityService.GetCsgoStyleLotteryDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (lotteryData == null)
                lotteryData = activityService.CreateDefaultCsgoStyleLotteryData(PlayerId, PlayerShard, activityId);

            var userAsset = await userAssetService.GetUserAssetsDetailedAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());

            var currentTime = TimeUtils.GetCurrentTime();
            if (TimeUtils.GetDayDiffBetween(currentTime, lotteryData.LotteryDrawInfoRefreshTimestamp, userAsset.TimeZoneOffset, 0) > 0)
            {
                lotteryData.KeyPurchaseCountByDiamond = 0;
                lotteryData.TotalLotteryDrawToday = 0;
                lotteryData.LotteryDrawInfoRefreshTimestamp = currentTime;
            }

            if (lotteryData.TotalLotteryDrawToday + CsgoLotteryTenDrawCount > drawLimitPerDay)
                return BadRequest(ErrorKind.CSGO_LOTTERY_RUN_OUT_TODAY.Response());
            
            var keyUsed = Math.Min(lotteryData.KeyCount, CsgoLotteryTenDrawCount);
            if (keyUsed > 0)
                lotteryData.KeyCount -= keyUsed;

            var needByDiamond = CsgoLotteryTenDrawCount - keyUsed;
            if (needByDiamond > 0)
            {
                int totalCost = 0;
                for (int i = 0; i < needByDiamond; i++)
                {
                    int costThisDraw = Math.Min(maxCost, baseCost + ((lotteryData.KeyPurchaseCountByDiamond + i) * costGrow));
                    totalCost += costThisDraw;
                }
                if (userAsset.DiamondCount < totalCost)
                    return BadRequest(ErrorKind.DIAMOND_NOT_ENOUGH.Response());

                lotteryData.KeyPurchaseCountByDiamond += needByDiamond;
                userAsset.DiamondCount -= totalCost;
            }
            
            var selectedRewardConfigs = new List<CsgoLotteryConfig>(CsgoLotteryTenDrawCount);
            var rewardRecordCount = lotteryData.RewardRecord.Count;
            // 十连抽内复用保底进度，避免每次抽奖重复遍历历史记录
            var distanceToLastQualifiedReward = new List<int>(Enumerable.Repeat(0, guaranteeList.Count));
            int qualityCheckStart = 0;
            for (int i = rewardRecordCount - 1; i >= 0; i--)
            {
                var pastRewardId = lotteryData.RewardRecord[i];
                var pastRewardConfig = serverConfig.GetCsgoLotteryConfigByActivityIdAndRewardId(activityId, pastRewardId);
                if (pastRewardConfig == null)
                    return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());
                if (pastRewardConfig.quality >= guaranteeList.Count - 1)
                    break;
                qualityCheckStart = Math.Max(qualityCheckStart, pastRewardConfig.quality);
                for (int quality = qualityCheckStart; quality < guaranteeList.Count; quality++)
                {
                    if (pastRewardConfig.quality < quality)
                        distanceToLastQualifiedReward[quality] += 1;
                }
            }

            for (int drawIndex = 0; drawIndex < CsgoLotteryTenDrawCount; drawIndex++)
            {
                int? forceQuality = null;
                for (int i = guaranteeList.Count - 1; i >= 0; i--)
                {
                    // "每X次必出" => 连续未出达到 X-1 时，下一抽强制该品质
                    if (guaranteeList[i] > 0 && distanceToLastQualifiedReward[i] >= guaranteeList[i] - 1)
                    {
                        forceQuality = i;
                        break;
                    }
                }

                var prizePool = FilterRewardPool(rewardRecordCount + drawIndex, lotteryConfigList, forceQuality);
                var randomlySelected = prizePool.WeightedRandomSelectOne(item => item.Weight)!;
                var rewardId = randomlySelected.ItemId;
                var rewardConfig = serverConfig.GetCsgoLotteryConfigByActivityIdAndRewardId(activityId, rewardId);
                if (rewardConfig == null)
                    return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());

                selectedRewardConfigs.Add(rewardConfig);
                for (int quality = 0; quality < guaranteeList.Count; quality++)
                {
                    if (rewardConfig.quality < quality)
                        distanceToLastQualifiedReward[quality] += 1;
                    else
                        distanceToLastQualifiedReward[quality] = 0;
                }
            }

            var finalReward = new GeneralReward() { ItemList = [], CountList = [] };
            var pointCount = 0;
            for (int i = 0; i < selectedRewardConfigs.Count; i++)
            {
                var rewardConfig = selectedRewardConfigs[i];

                finalReward.ItemList.AddRange(rewardConfig.item_list);
                finalReward.CountList.AddRange(rewardConfig.count_list);

                pointCount += rewardConfig.point_reward;
            }
            finalReward.ItemList.Add((int)MoneyType.CsgoLotteryPoint);
            finalReward.CountList.Add(pointCount);

            lotteryData.TotalLotteryDrawToday += CsgoLotteryTenDrawCount;

            for (int i = 0; i < selectedRewardConfigs.Count; i++)
            {
                var rewardConfig = selectedRewardConfigs[i];
                lotteryData.RewardRecord.Add(rewardConfig.reward_id);
                lotteryData.RewardRecordTime.Add(currentTime);
            }
            finalReward.DistinctAndMerge();
            TrimCsgoLotteryRewardRecord(lotteryData);

            var (newCardList, takeRewardResult) = await userItemService.TakeReward(userAsset, finalReward, GameVersion);
            if (takeRewardResult == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

            activityService.RecordCsgoStyleLotteryTask(lotteryData, ActivityTaskKeys.DrawCsgoLottery, CsgoLotteryTenDrawCount, userAsset.TimeZoneOffset);

            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            takeRewardResult.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            return Ok(new CsgoLotteryDrawReply
            {
                LotteryData = lotteryData.ToClientApi(userAsset),
                RewardResult = takeRewardResult
            });
        });
    }

    /// <summary>
    /// Csgo风格抽奖 - 领取积分奖励
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TakeRewardResult>> ClaimCsgoStyleLotteryPointReward(int rewardIndex)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivityCsgoStyleLottery, GameVersion);
        if (activity == null)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());
        var activityId = activity.id;

        var pointRewardConfigList = serverConfig.GetCsgoLotteryPointConfigListByActivityId(activityId);
        if (pointRewardConfigList == null || rewardIndex < 0 || rewardIndex >= pointRewardConfigList.Count)
            return BadRequest(ErrorKind.INVALID_INPUT.Response());

        var rewardConfig = pointRewardConfigList[rewardIndex];
        
        return await dbContext.WithRCUDefaultRetry<ActionResult<TakeRewardResult>>(async _ =>
        {
            var lotteryData = await activityService.GetCsgoStyleLotteryDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (lotteryData == null)
                lotteryData = activityService.CreateDefaultCsgoStyleLotteryData(PlayerId, PlayerShard, activityId);

            // 检查积分是否足够
            if (lotteryData.ActivityPoint < rewardConfig.point_required)
                return BadRequest(ErrorKind.INVALID_INPUT.Response());

            // 检查是否已领取
            if ((lotteryData.PointRewardClaimStatus & (1L << rewardIndex)) > 0)
                return BadRequest(ErrorKind.REWARD_CLAIMED.Response());

            // 标记为已领取
            lotteryData.PointRewardClaimStatus |= (1L << rewardIndex);

            // 发放奖励
            var includeOption = userItemService.CalculateUserAssetIncludeOptions(rewardConfig.item_list);
            var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());

            var generalReward = new GeneralReward() { ItemList = rewardConfig.item_list.ToList(), CountList = rewardConfig.count_list.ToList() };
            var (newCardList, takeRewardResult) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            if (takeRewardResult == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

            // 使用事务确保一致性
            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (takeRewardResult.AssetsChange != null)
                takeRewardResult.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            return Ok(takeRewardResult);
        });
    }

    /// <summary>
    /// Csgo风格抽奖 - 领取任务奖励
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CsgoLotteryDrawReply>> ClaimCsgoStyleLotteryTaskReward(string taskKey)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivityCsgoStyleLottery, GameVersion);
        if (activity == null)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());
        var activityId = activity.id;

        var taskConfig = serverConfig.GetCsgoLotteryTaskConfigByKey(activityId, taskKey);
        if (taskConfig == null)
            return BadRequest(ErrorKind.CONFIG_NOT_FOUND.Response());

        return await dbContext.WithRCUDefaultRetry<ActionResult<CsgoLotteryDrawReply>>(async _ =>
        {
            var lotteryData = await activityService.GetCsgoStyleLotteryDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (lotteryData == null)
                lotteryData = activityService.CreateDefaultCsgoStyleLotteryData(PlayerId, PlayerShard, activityId);

            if (!lotteryData.TaskRecord.TryGetValue(taskKey, out var record))
            {
                record = new CsgoStyleLotteryTask()
                {
                    Progress = 0,
                    Claimed = false,
                    UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                };
                lotteryData.TaskRecord.Add(taskKey, record);
            }

            var includeOption = userItemService.CalculateUserAssetIncludeOptions([taskConfig.reward_item]);
            var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());

            // 检查是否跨天了（日常任务重置）
            var currentTime = TimeUtils.GetCurrentTime();
            if (taskConfig.is_daily && TimeUtils.GetDayDiffBetween(currentTime, record.UpdatedAt, userAsset.TimeZoneOffset, 0) > 0)
            {
                record.Progress = 0;
                record.Claimed = false;
            }

            // 检查是否可以领奖
            if (record.Claimed)
                return BadRequest(ErrorKind.REWARD_CLAIMED.Response());
            if (record.Progress < taskConfig.target_progress)
                return BadRequest(ErrorKind.CSGO_TASK_NOT_FINISHED.Response());

            record.Claimed = true;
            dbContext.Entry(lotteryData).Property(t => t.TaskRecord).IsModified = true;

            // 发放奖励
            var generalReward = new GeneralReward() { ItemList = [taskConfig.reward_item], CountList = [taskConfig.reward_count] };
            var (newCardList, takeRewardResult) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            if (takeRewardResult == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

            // 使用事务确保一致性
            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (takeRewardResult.AssetsChange != null)
                takeRewardResult.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            return Ok(new CsgoLotteryDrawReply
            {
                LotteryData = lotteryData.ToClientApi(userAsset),
                RewardResult = takeRewardResult,
            });
        });
    }

    /// <summary>
    /// 手动刷新csgo风格抽奖数据
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<FetchCsgoLotteryDataReply>> FetchCsgoLotteryData()
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivityCsgoStyleLottery, GameVersion);
        if (activity == null)
        {
            return await dbContext.WithRCUDefaultRetry<ActionResult<FetchCsgoLotteryDataReply>>(async _ =>
            {
                var timeConfigs = activityService.GetActivitiesByType(ActivityType.ActivityCsgoStyleLottery);
                List<int> passRewardItems = new();
                foreach (var timeConfig in timeConfigs)
                {
                    var passConfigs = serverConfig.GetCsgoLotteryPassConfigListByActivityId(timeConfig.id);
                    foreach (var passConfig in passConfigs)
                    {
                        passRewardItems.AddRange(passConfig.daily_reward_item);
                    }
                }
                var includeOption = userItemService.CalculateUserAssetIncludeOptions(passRewardItems);
                var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(
                    PlayerShard, PlayerId, includeOption);
                if (userAsset == null)
                    return BadRequest(ErrorKind.NO_USER_ASSET.Response());
                var (databaseChanged, expireRewardResult) =
                    await HandleExpiredCsgoLottery(userAsset);

                if (databaseChanged)
                {
                    await using var t = await dbContext.Database.BeginTransactionAsync();
                    await dbContext.SaveChangesWithDefaultRetryAsync(false);
                    await t.CommitAsync();
                    dbContext.ChangeTracker.AcceptAllChanges();
                }

                return Ok(new FetchCsgoLotteryDataReply(
                    null, null,
                    expireRewardResult));
            });
        }
        var activityId = activity.id;
        
        return await dbContext.WithRCUDefaultRetry<ActionResult<FetchCsgoLotteryDataReply>>(async _ =>
        {
            var lotteryData = await activityService.GetCsgoStyleLotteryDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (lotteryData == null)
                lotteryData = activityService.CreateDefaultCsgoStyleLotteryData(PlayerId, PlayerShard, activityId);
            List<int> itemList = new List<int>();
            foreach (var passConfig in serverConfig.GetCsgoLotteryPassConfigListByActivityId(activityId))
                itemList.AddRange(passConfig.daily_reward_item);
            var includeOption = userItemService.CalculateUserAssetIncludeOptions(itemList);
            UserAssets? userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(
                PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());

            bool databaseChanged = false;

            databaseChanged |= activityService.CheckRefreshCsgoStyleLotteryTask(lotteryData, userAsset.TimeZoneOffset);
            databaseChanged |= activityService.RecordCsgoStyleLotteryTask(
                lotteryData, ActivityTaskKeys.Login, 1, userAsset.TimeZoneOffset);
            databaseChanged |= activityService.RecordCsgoStyleLotteryTask(
                lotteryData, ActivityTaskKeys.LoginDaily, 1, userAsset.TimeZoneOffset);

            var (passRewardChanged, passRewardResult) =
                await ClaimCsgoLotteryPassDailyRewards(lotteryData, activity, userAsset);
            databaseChanged |= passRewardChanged;

            if (databaseChanged)
            {
                await using var t = await dbContext.Database.BeginTransactionAsync();
                await dbContext.SaveChangesWithDefaultRetryAsync(false);
                await t.CommitAsync();
                dbContext.ChangeTracker.AcceptAllChanges();
            }

            return Ok(new FetchCsgoLotteryDataReply(
                lotteryData.ToClientApi(userAsset),
                passRewardResult,
                null));
        });
    }

    /// <summary>
    /// 领取CSGO lottery pass的每日奖励
    /// </summary>
    private async Task<(bool, TakeRewardResult?)> ClaimCsgoLotteryPassDailyRewards(
        ActivityCsgoStyleLottery lotteryData,
        ActivityTimeConfig activity,
        UserAssets userAsset)
    {
        bool databaseChanged = false;
        var passConfigList = serverConfig.GetCsgoLotteryPassConfigListByActivityId(activity.id);
        if (passConfigList.Count == 0)
            return (false, null);

        var totalReward = new GeneralReward();
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var activityStartTime = TimeUtils.ParseDateTimeStrToUnixSecond(activity.start_time);

        for (int level = 0; level < passConfigList.Count; level++)
        {
            var passConfig = passConfigList[level];
            // 检查是否购买了该等级的通行证
            if ((lotteryData.ActivityPremiumPassStatus & (1L << level)) == 0)
                continue;

            // 获取上次领取的天数
            long lastClaimTime = activityStartTime;
            if (lotteryData.PremiumPassDailyRewardClaimStatus.Count > level)
                lastClaimTime = lotteryData.PremiumPassDailyRewardClaimStatus[level];

            // 发放从上次领取到现在的所有每日奖励
            var dayDiff = TimeUtils.GetDayDiffBetween(currentTime, lastClaimTime, userAsset.TimeZoneOffset, 0);
            if (dayDiff > 0)
            {
                for (int ri = 0; ri < passConfig.daily_reward_item.Count; ri++)
                {
                    totalReward.AddReward(passConfig.daily_reward_item[ri],
                        passConfig.daily_reward_count[ri] * dayDiff);
                }
                databaseChanged = true;
            }

            // 更新领取状态
            while (lotteryData.PremiumPassDailyRewardClaimStatus.Count <= level)
                lotteryData.PremiumPassDailyRewardClaimStatus.Add(activityStartTime);
            lotteryData.PremiumPassDailyRewardClaimStatus[level] = currentTime;
        }
        
        totalReward.DistinctAndMerge();
        if (!databaseChanged || totalReward.ItemList.Count == 0)
            return (false, null);

        var (_, rewardResult) = await userItemService.TakeReward(userAsset, totalReward, GameVersion);
        return (true, rewardResult);
    }
    
    /// <summary>
    /// 处理过期的CSGO lottery：
    /// 1. 领取所有未领取的通行证奖励
    /// 2. 退还未消耗的钥匙
    /// </summary>
    public async Task<(bool, TakeRewardResult?)> HandleExpiredCsgoLottery(UserAssets userAsset)
    {
        bool databaseChanged = false;
        var csgoLotteryKeyConfig = serverConfig.GetItemConfigById((int)(MoneyType.CsgoStyleLottery));
        if (csgoLotteryKeyConfig == null)
            return (false, null);

        return await dbContext.WithRCUDefaultRetry<(bool, TakeRewardResult?)>(async _ =>
        {
            var totalReward = new GeneralReward();
            var dataList = await activityService.GetCsgoLotteryDataList(PlayerId, PlayerShard, TrackingOptions.Tracking);
            foreach (var data in dataList)
            {
                var timeConfig = serverConfig.GetActivityConfigById(data.ActivityId);
                // 这里不使用客户端版本，以防有玩家同时运行两个版本
                if (timeConfig == null || activityService.IsOpen(timeConfig, "100.0.0"))
                    continue;
                
                // 软删除
                data.DeletedAt = DateTime.UtcNow;

                var startTime = TimeUtils.ParseDateTimeStrToUnixSecond(timeConfig.start_time);
                var endTime = TimeUtils.ParseDateTimeStrToUnixSecond(timeConfig.end_time);
                
                // 1. 领取所有未领取的通行证奖励
                var passConfigList = serverConfig.GetCsgoLotteryPassConfigListByActivityId(data.ActivityId);
                for (int passLevel = 0; passLevel < passConfigList.Count; passLevel++)
                {
                    if ((data.ActivityPremiumPassStatus & (1 << passLevel)) == 0) continue;
                    var lastClaimTime = data.PremiumPassDailyRewardClaimStatus.Count > passLevel
                        ? data.PremiumPassDailyRewardClaimStatus[passLevel]
                        : startTime;
                    var dayDiff = TimeUtils.GetDayDiffBetween(endTime, lastClaimTime, userAsset.TimeZoneOffset, 0);
                    if (dayDiff > 0)
                    {
                        var passConfig = passConfigList[passLevel];
                        for (int ri = 0; ri < passConfig.daily_reward_item.Count; ri++)
                        {
                            var rewardCount = passConfig.daily_reward_count[ri] * dayDiff;
                            // 特例：钥匙直接加到钥匙数量里
                            if (passConfig.daily_reward_item[ri] == (int)MoneyType.CsgoStyleLottery)
                                data.KeyCount += rewardCount;
                            else
                                totalReward.AddReward(passConfig.daily_reward_item[ri], rewardCount);
                        }
                        databaseChanged = true;
                    }
                }

                // 2. 退还未消耗的钥匙
                if (data.KeyCount > 0)
                {
                    int diamondRefund = (int)(data.KeyCount * csgoLotteryKeyConfig.diamond_value);
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
    
    private List<RewardPoolItem> FilterRewardPool(
        int drawTime,
        List<CsgoLotteryConfig> csgoLotteryConfig,
        int? guaranteedQuality = null)
    {
        if (!serverConfig.TryGetParameterInt(Params.CsgoLotteryTeaserOffThreshold, out var teaserThresh) ||
            !serverConfig.TryGetParameterFloat(Params.CsgoLotteryTeaserPower, out var teaserPower) ||
            !serverConfig.TryGetParameterInt(Params.CsgoLotteryMinGoodQuality, out var minGoodQuality))
        {
            teaserThresh = -1;
            teaserPower = 1.0f;
            minGoodQuality = int.MaxValue;
        }
        var rewardPool = new List<RewardPoolItem>();
        for (int i = 0; i < csgoLotteryConfig.Count; i++)
        {
            var rewardConfig = csgoLotteryConfig[i];
            if (guaranteedQuality.HasValue && guaranteedQuality > rewardConfig.quality)
                continue;
            var weight = rewardConfig.probability;
            if (drawTime < teaserThresh &&
                rewardConfig.quality >= minGoodQuality)
            {
                weight = (int)(teaserPower * weight);
            }
            rewardPool.Add(new RewardPoolItem() { ItemId = rewardConfig.reward_id, Weight = weight });
        }

        return rewardPool;
    }

    private static void TrimCsgoLotteryRewardRecord(ActivityCsgoStyleLottery data)
    {
        var excess = data.RewardRecord.Count - CsgoLotteryRecordMax;
        if (excess <= 0)
            return;

        // Remove oldest records from the front to keep the latest items.
        data.RewardRecord.RemoveRange(0, excess);
        data.RewardRecordTime.RemoveRange(0, excess);
    }
}
