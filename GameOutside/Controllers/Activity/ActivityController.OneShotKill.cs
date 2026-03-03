using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.Util;
using Microsoft.AspNetCore.Mvc;

public partial class ActivityController
{
    [HttpPost]
    public async Task<ActionResult<TakeRewardResult>> ClaimOneShotKillMapConquerReward(int activityId, int level)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivityOneShotKill, GameVersion);
        if (activity == null || activity.id != activityId)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());

        var mapConfigList = serverConfig.GetOneShotKillMapConfigListByActivityId(activityId);
        if (level >= mapConfigList.Count)
            return BadRequest(ErrorKind.NO_MAP_CONFIG.Response());
        var mapConfig = mapConfigList[level];

        return await dbContext.WithRCUDefaultRetry<ActionResult<TakeRewardResult>>(async _ =>
        {
            GeneralReward generalReward = new GeneralReward();
            for (int i = 0; i < mapConfig.conquer_reward_list.Count; i++)
                generalReward.AddReward(mapConfig.conquer_reward_list[i], mapConfig.conquer_reward_count[i]);
            var includeOption = userItemService.CalculateUserAssetIncludeOptions(mapConfig.conquer_reward_list);
            var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());

            var oneShotKillData = await activityService.GetOneShotKillDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (oneShotKillData == null)
                oneShotKillData = activityService.CreateDefaultOneShotKillData(PlayerId, PlayerShard, activityId);
            var lastClaimTime = oneShotKillData.MapConquerRewardClaimTimestamp.Count > level
                ? oneShotKillData.MapConquerRewardClaimTimestamp[level]
                : 0;
            if (TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(), lastClaimTime, userAsset.TimeZoneOffset, 0) <= 0)
                return BadRequest(ErrorKind.REWARD_CLAIMED.Response()); // 玩家已经领取了这个奖励
            var mapProgress = await activityService.GetOneShotKillMapProgressAsync(activityId);
            if (level >= mapProgress.Count)
                return BadRequest(ErrorKind.ONE_SHOT_KILL_REGION_NOT_CONQUERED.Response()); // 该关卡还没有被征服
            if (mapProgress[level] < mapConfig.victory_count_to_conquer)
                return BadRequest(ErrorKind.ONE_SHOT_KILL_REGION_NOT_CONQUERED.Response()); // 该关卡还没有被征服

            // 检测完了，发奖励
            var (newCardList, takeRewardResult) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            while (oneShotKillData.MapConquerRewardClaimTimestamp.Count <= level)
                oneShotKillData.MapConquerRewardClaimTimestamp.Add(0);
            oneShotKillData.MapConquerRewardClaimTimestamp[level] = TimeUtils.GetCurrentTime();

            // 使用事务确保一致性
            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (takeRewardResult?.AssetsChange != null)
                takeRewardResult.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            return Ok(takeRewardResult);
        });
    }

    [HttpPost]
    public async Task<ActionResult<TakeRewardResult>> ClaimOneShotKillUltimateReward(int activityId)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivityOneShotKill, GameVersion);
        if (activity == null || activity.id != activityId)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());

        return await dbContext.WithRCUDefaultRetry<ActionResult<TakeRewardResult>>(async _ =>
        {
            var oneShotKillData = await activityService.GetOneShotKillDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (oneShotKillData == null) // 领取最终奖励不需要玩家完成过活动，所以没有data的时候直接创建一个
                oneShotKillData = activityService.CreateDefaultOneShotKillData(PlayerId, PlayerShard, activityId);
            if (oneShotKillData.OneShotKillUltimateRewardClaimStatus)
                return BadRequest(ErrorKind.REWARD_CLAIMED.Response()); // 已经领过了
            var mapProgress = await activityService.GetOneShotKillMapProgressAsync(activityId);
            var mapConfigList = serverConfig.GetOneShotKillMapConfigListByActivityId(activityId);
            foreach (var mapConfig in mapConfigList)
            {
                var level = mapConfig.level;
                if (level >= mapProgress.Count)
                    return BadRequest(ErrorKind.ONE_SHOT_KILL_REGION_NOT_ALL_CONQUERED.Response()); // 该关卡还没有被征服
                if (mapProgress[level] < mapConfig.victory_count_to_conquer)
                    return BadRequest(ErrorKind.ONE_SHOT_KILL_REGION_NOT_ALL_CONQUERED.Response()); // 该关卡还没有被征服
            }

            // 检测完了，发奖励
            GeneralReward ultimateRewardConfig = serverConfig.GetOneShotKillUltimateRewardByActivityId(activityId);
            GeneralReward generalReward = new GeneralReward()
            {
                ItemList = ultimateRewardConfig.ItemList.ToList(),
                CountList = ultimateRewardConfig.CountList.ToList()
            };
            var includeOption = userItemService.CalculateUserAssetIncludeOptions(ultimateRewardConfig.ItemList);
            var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            var (newCardList, takeRewardResult) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            oneShotKillData.OneShotKillUltimateRewardClaimStatus = true;

            // 使用事务确保一致性
            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (takeRewardResult?.AssetsChange != null)
                takeRewardResult.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();
            return Ok(takeRewardResult);
        });
    }

    [HttpPost]
    public async Task<ActionResult<TakeRewardResult>> ClaimOneShotKillTaskReward(int activityId, int taskId)
    {
        var activity = activityService.GetOpeningActivityByType(ActivityType.ActivityOneShotKill, GameVersion);
        if (activity == null || activity.id != activityId)
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());

        var taskConfigList = serverConfig.GetOneShotKillTaskConfigListByActivityId(activityId);
        if (taskId >= taskConfigList.Count)
            return BadRequest(ErrorKind.NO_MAP_CONFIG.Response());
        var taskConfig = taskConfigList[taskId];

        return await dbContext.WithRCUDefaultRetry<ActionResult<TakeRewardResult>>(async _ =>
        {
            GeneralReward generalReward = new GeneralReward();
            for (int i = 0; i < taskConfig.reward_list.Count; i++)
                generalReward.AddReward(taskConfig.reward_list[i], taskConfig.reward_count[i]);
            var includeOption = userItemService.CalculateUserAssetIncludeOptions(taskConfig.reward_list);
            var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());

            var oneShotKillData = await activityService.GetOneShotKillDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.Tracking);
            if (oneShotKillData == null)
                oneShotKillData = activityService.CreateDefaultOneShotKillData(PlayerId, PlayerShard, activityId);
            if ((oneShotKillData.TaskCompleteRewardClaimStatus & (1 << taskId)) > 0)
                return BadRequest(ErrorKind.REWARD_CLAIMED.Response()); // 玩家已经领取了这个奖励
            var taskProgress = await activityService.GetOneShotKillTaskProgressAsync(activityId);
            if (taskId >= taskProgress.Count)
                return BadRequest(ErrorKind.ONE_SHOT_KILL_REGION_NOT_CONQUERED.Response()); // 该任务还没有被完成
            if (taskProgress[taskId] < taskConfig.count_to_complete)
                return BadRequest(ErrorKind.ONE_SHOT_KILL_REGION_NOT_CONQUERED.Response()); // 该任务还没有被完成

            // 检测完了，发奖励
            var (newCardList, takeRewardResult) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            oneShotKillData.TaskCompleteRewardClaimStatus |= (long)1 << taskId;

            // 使用事务确保一致性
            await using var t = await dbContext.Database.BeginTransactionAsync();
            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(newCardList, PlayerShard, PlayerId);
            if (takeRewardResult?.AssetsChange != null)
                takeRewardResult.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            return Ok(takeRewardResult);
        });
    }

    [HttpGet]
    public async Task<ActionResult<OneShotKillDataClient?>> RefreshOneShotKillData()
    {
        OneShotKillDataClient? result = null;
        var config = activityService.GetOpeningActivityByType(ActivityType.ActivityOneShotKill, GameVersion);
        if (config == null)
            return Ok(result);

        int activityId = config.id;
        var userAsset = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
        if (userAsset == null)
            return BadRequest(ErrorKind.NO_USER_ASSET.Response());

        var oneShotKillData = await activityService.GetOneShotKillDataAsync(PlayerId, PlayerShard, activityId, TrackingOptions.NoTracking);
        if (oneShotKillData == null)
            oneShotKillData = DataUtil.DefaultOneShotKillData(PlayerId, PlayerShard, activityId);
        var currentTime = TimeUtils.GetCurrentTime();

        var mapProgress = await activityService.GetOneShotKillMapProgressAsync(activityId);
        var taskProgress = await activityService.GetOneShotKillTaskProgressAsync(activityId);
        var progressAdd = await activityService.GetOneShotKillLocalProgressAddAsync(activityId);
        var globalProgressAdd = activityService.GetOneShotKillGlobalProgressAdd(activityId, taskProgress);
        progressAdd = progressAdd.Select(i => i + globalProgressAdd).ToList();
        var progressLose = await activityService.GetOneShotKillLocalProgressMinusAsync(activityId);
        var eventList = await activityService.GetOneShotKillEventListAsync(activityId);
        result = oneShotKillData.ToClientApi(mapProgress, taskProgress, progressAdd, progressLose, eventList);

        if (TimeUtils.GetDayDiffBetween(currentTime, oneShotKillData.NormalVictoryUpdateTimestamp,
                userAsset.TimeZoneOffset,
                0) > 0)
            result.NormalVictoryToday = 0;
        if (TimeUtils.GetDayDiffBetween(currentTime, oneShotKillData.ChallengeVictoryUpdateTimestamp,
                userAsset.TimeZoneOffset,
                0) > 0)
            result.ChallengeVictoryToday = 0;
        result.EventList = (await activityService.GetOneShotKillEventListAsync(activityId)).Select(
            item => new OneShotKillEvent()
            {
                EventHash = (int)item.Name,
                Timestamp = (long)item.Value,
            }).ToList();
        return Ok(result);
    }
}