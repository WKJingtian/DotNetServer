using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.ApiController;
using AssistActivity.Services;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Services;
using GameOutside.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static AssistActivity.Services.AssistActivityManagementService;

namespace GameOutside.Controllers;

[Authorize]
public class TaskController(
    IConfiguration configuration,
    ILogger<TaskController> logger,
    ServerConfigService serverConfigService,
    UserItemService userItemService,
    BuildingGameDB context,
    AssistActivityManagementService assistActivityManagementService,
    UserAssetService userAssetService,
    InvitationCodeService invitationCodeService,

    UserAchievementService userAchievementService
)
    : BaseApiController(configuration)
{
    [HttpPost]
    public async Task<ActionResult<UserBeginnerTask>> GetBeginnerTask()
    {
        return await context.WithRCUDefaultRetry<ActionResult<UserBeginnerTask>>(async _ =>
        {
            var beginnerTask = await context.GetBeginnerTaskAsync(PlayerShard, PlayerId);
            if (beginnerTask == null)
            {
                beginnerTask = new UserBeginnerTask()
                {
                    ShardId = PlayerShard,
                    PlayerId = PlayerId,
                    FinishedCount = 0,
                    Received = false,
                    StartTime = TimeUtils.GetCurrentTime(),
                    DayIndex = 0,
                    TaskList = new(),
                };
                // 随机三个任务

                var taskList = RandomBeginnerTasks(3, beginnerTask.DayIndex);
                beginnerTask.TaskList = taskList;
                context.Entry(beginnerTask).Property(t => t.TaskList).IsModified = true;
                context.AddBeginnerTask(beginnerTask);

                await context.SaveChangesWithDefaultRetryAsync();
            }

            return Ok(beginnerTask);
        });
    }

    public record struct TakeTaskRewardReply(int NewCoin, int NewDiamond, UserBeginnerTask UserTaskData);

    [HttpPost]
    public async Task<ActionResult<TakeTaskRewardReply>> TakeTaskReward(int index)
    {
        return await context.WithRCUDefaultRetry<ActionResult<TakeTaskRewardReply>>(async _ =>
        {
            var userTaskData = await context.GetBeginnerTaskAsync(PlayerShard, PlayerId);
            if (userTaskData == null)
                return BadRequest(ErrorKind.NO_BEGINNER_TASK_RECORD.Response());

            // 未找到配置
            var dayConfig = serverConfigService.GetBeginnerTaskDayConfig(userTaskData.DayIndex);
            if (dayConfig == null)
                return BadRequest(ErrorKind.NO_DAY_TASK_CONFIG.Response());

            // 检查时间是否到了目标时间
            if (!serverConfigService.TryGetParameterInt(Params.TaskUpdateTimeOffset, out int offset))
                return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());

            var userAsset = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            var currentTime = TimeUtils.GetCurrentTime();
            int dayOffset =
                TimeUtils.GetDayDiffBetween(currentTime, userTaskData.StartTime, userAsset.TimeZoneOffset, offset);
            if (dayOffset < userTaskData.DayIndex)
                return BadRequest(ErrorKind.NOT_TASK_TIME_YET.Response());

            // 无该任务
            if (index >= userTaskData.TaskList.Count)
                return BadRequest(ErrorKind.NO_TASK_CONFIG.Response());
            var task = userTaskData.TaskList[index];
            var taskConfig = serverConfigService.GetBeginnerTaskConfig(task.Id);
            if (taskConfig == null)
                return BadRequest(ErrorKind.NO_TASK_CONFIG.Response());
            // 进度未达成
            var targetProgress = taskConfig.target_progress;
            if (task.Progress < targetProgress)
                return BadRequest(ErrorKind.TASK_NOT_COMPLETE.Response());

            var excludeTaskSet = new HashSet<int>(userTaskData.TaskList.Select(t => t.Id).ToList());
            userAsset.CoinCount += taskConfig.coin;
            userAsset.DiamondCount += taskConfig.diamond;
            // 刷新任务
            userTaskData.TaskList.Remove(task);
            userTaskData.FinishedCount++;

            // 补新的任务
            if (userTaskData.FinishedCount + userTaskData.TaskList.Count < dayConfig.task_count)
            {
                userTaskData.TaskList.Add(RandomOneBeginnerTask(userTaskData.DayIndex, excludeTaskSet));
            }

            context.Entry(userTaskData).Property(t => t.TaskList).IsModified = true;

            // 使用事务确保一致性
            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            var result = new TakeTaskRewardReply()
            {
                NewCoin = userAsset.CoinCount,
                NewDiamond = userAsset.DiamondCount,
                UserTaskData = userTaskData,
            };
            return Ok(result);
        });
    }

    public record struct ReRollTaskReply(int NewCoin, List<BeginnerTaskData> TaskList);

    [HttpPost]
    public async Task<ActionResult<ReRollTaskReply>> ReRollTask(int index)
    {
        return await context.WithRCUDefaultRetry<ActionResult<ReRollTaskReply>>(async _ =>
        {
            var userTaskData = await context.GetBeginnerTaskAsync(PlayerShard, PlayerId);
            if (userTaskData == null)
                return BadRequest(ErrorKind.NO_BEGINNER_TASK_RECORD.Response());

            // 未找到配置
            var dayConfig = serverConfigService.GetBeginnerTaskDayConfig(userTaskData.DayIndex);
            if (dayConfig == null)
                return BadRequest(ErrorKind.NO_DAY_TASK_CONFIG.Response());

            // 无该任务
            if (index >= userTaskData.TaskList.Count)
                return BadRequest(ErrorKind.NO_TASK_CONFIG.Response());
            var task = userTaskData.TaskList[index];
            var taskConfig = serverConfigService.GetBeginnerTaskConfig(task.Id);
            if (taskConfig == null)
                return BadRequest(ErrorKind.NO_TASK_CONFIG.Response());

            // 检查消耗
            if (!serverConfigService.TryGetParameterInt(Params.BeginnerTaskRerollCoinCost, out int coinCost))
                return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());

            // 扣除重刷费用
            var userAsset = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
            if (userAsset is null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());

            if (userAsset.CoinCount < coinCost)
                return BadRequest(ErrorKind.COIN_NOT_ENOUGH.Response());
            userAsset.CoinCount -= coinCost;

            var excludeTaskSet = new HashSet<int>(userTaskData.TaskList.Select(t => t.Id).ToList());
            // 重刷该任务
            var newTask = RandomOneBeginnerTask(userTaskData.DayIndex, excludeTaskSet);
            userTaskData.TaskList[index] = newTask;
            context.Entry(userTaskData).Property(t => t.TaskList).IsModified = true;

            // 使用事务确保一致性
            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            return Ok(new ReRollTaskReply() { NewCoin = userAsset.CoinCount, TaskList = userTaskData.TaskList });
        });
    }

    public record struct TakeDayRewardReply(TakeRewardResult TakeRewardResult, UserBeginnerTask UserTaskData);

    // 领取当日任务奖励后，会将DayIndex+1(除非当前天数是最后一天，然后重新生成当天的任务
    // 领奖和任务统计的时候拦截一下是不是当前天数的任务已激活
    [HttpPost]
    public async Task<ActionResult<TakeDayRewardReply>> TakeDayReward()
    {
        return await context.WithRCUDefaultRetry<ActionResult<TakeDayRewardReply>>(async _ =>
        {
            var userTaskData = await context.GetBeginnerTaskAsync(PlayerShard, PlayerId);
            if (userTaskData == null)
                return BadRequest(ErrorKind.NO_BEGINNER_TASK_RECORD.Response());

            // 奖励已被领取了
            if (userTaskData.Received)
                return BadRequest(ErrorKind.DAY_TASK_REWARD_RECEIVED.Response());

            var dayConfig = serverConfigService.GetBeginnerTaskDayConfig(userTaskData.DayIndex);
            if (dayConfig == null)
                return BadRequest(ErrorKind.NO_DAY_TASK_CONFIG.Response());

            // 当日任务未完成
            if (userTaskData.FinishedCount < dayConfig.task_count)
                return BadRequest(ErrorKind.TASK_NOT_COMPLETE.Response());

            // 检查时间是否到了目标时间
            if (!serverConfigService.TryGetParameterInt(Params.TaskUpdateTimeOffset, out int offset))
                return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());

            var generalReward =
                new GeneralReward() { ItemList = [dayConfig.reward_item], CountList = [dayConfig.reward_count] };

            var includeOption = userItemService.CalculateUserAssetIncludeOptions(generalReward.ItemList);
            var userAsset
                = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            var currentTime = TimeUtils.GetCurrentTime();
            int dayOffset =
                TimeUtils.GetDayDiffBetween(currentTime, userTaskData.StartTime, userAsset.TimeZoneOffset, offset);
            if (dayOffset < userTaskData.DayIndex)
                return BadRequest(ErrorKind.NOT_TASK_TIME_YET.Response());

            // 领奖
            var (newCardList, takeRewardResult) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            if (takeRewardResult == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());
            // 进入下一天
            userTaskData.Received = true;
            int maxDayCount = serverConfigService.GetBeginnerTaskMaxDayCount();
            if (userTaskData.DayIndex + 1 < maxDayCount)
            {
                userTaskData.DayIndex++;
                userTaskData.Received = false;
                userTaskData.FinishedCount = 0;
                userTaskData.TaskList = RandomBeginnerTasks(3, userTaskData.DayIndex);
                context.Entry(userTaskData).Property(t => t.TaskList).IsModified = true;
            }

            // 使用事务确保一致性
            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(
                newCardList, PlayerShard, PlayerId
            );
            if (takeRewardResult.AssetsChange != null)
                takeRewardResult.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();

            var reply = new TakeDayRewardReply() { TakeRewardResult = takeRewardResult, UserTaskData = userTaskData, };
            return Ok(reply);
        });
    }

    private List<BeginnerTaskData> RandomBeginnerTasks(int count, int day)
    {
        var configList = serverConfigService.GetBeginnerTaskList();
        var selectionList = configList.Where(config => !config.key.Equals("soldier_max_level") || day >= 2).ToList();
        var beginnerTaskConfig = serverConfigService.GetBeginnerTaskDayConfig(day);
        var result = new List<BeginnerTaskData>();
        int i = 0;
        if (beginnerTaskConfig != null)
            for (; i < count && i < beginnerTaskConfig.predefined_tasks.Length; i++)
            {
                var predefinedTask = serverConfigService.GetBeginnerTaskConfig(beginnerTaskConfig.predefined_tasks[i]);
                if (predefinedTask == null)
                    continue;
                selectionList.RemoveAll(config => config.key == predefinedTask.key);
                result.Add(new BeginnerTaskData() { Id = predefinedTask.id, Progress = 0, });
            }

        for (; i < count && selectionList.Count > 0; i++)
        {
            var randomOne = selectionList.WeightedRandomSelectOne(_ => 10)!;
            selectionList.RemoveAll(config => config.key == randomOne.key);

            result.Add(new BeginnerTaskData() { Id = randomOne.id, Progress = 0, });
        }

        return result;
    }

    private BeginnerTaskData RandomOneBeginnerTask(int day, HashSet<int>? excludeTaskSet)
    {
        var configList = serverConfigService.GetBeginnerTaskList();
        var selectionList = configList.Where(config =>
                (excludeTaskSet != null && !excludeTaskSet.Contains(config.id)) &&
                (!config.key.Equals("soldier_max_level") || day >= 2))
            .ToList();
        var randomOne = selectionList.WeightedRandomSelectOne(_ => 1)!;
        return new BeginnerTaskData() { Id = randomOne.id, Progress = 0, };
    }

    public record struct DailyTaskStatusReply(
        List<int> progress,
        int taskRewardClaimStatus,
        int activeScoreRewardClaimStatus);

    [HttpPost]
    public async Task<ActionResult<DailyTaskStatusReply?>> FetchDailyTaskStatus()
    {
        return await context.WithRCUDefaultRetry<ActionResult<DailyTaskStatusReply?>>(async _ =>
        {
            var timeZoneOffset = await userAssetService.GetTimeZoneOffsetAsync(PlayerShard, PlayerId);
            if (timeZoneOffset is null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            var (taskStatus, taskDataChanged) = await context.AddDailyTaskProgress(serverConfigService,
                DailyTaskType.LOG_IN, 1,
                PlayerShard, PlayerId, timeZoneOffset.Value);

            if (taskDataChanged)
            {
                // 单表更新
                await context.SaveChangesWithDefaultRetryAsync();
            }

            return Ok(new DailyTaskStatusReply()
            {
                progress = taskStatus.TaskProgress,
                taskRewardClaimStatus = taskStatus.DailyTaskRewardClaimStatus,
                activeScoreRewardClaimStatus = taskStatus.ActiveScoreRewardClaimStatus,
            });
        });
    }

    [HttpPost]
    public async Task<ActionResult<int>> ClaimAllDailyTaskReward()
    {
        return await context.WithRCUDefaultRetry<ActionResult<int>>(async _ =>
        {
            var timeZoneOffset = await userAssetService.GetTimeZoneOffsetAsync(PlayerShard, PlayerId);
            if (timeZoneOffset is null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            var taskStatus = await context.GetDailyTask(PlayerShard, PlayerId);
            if (taskStatus == null)
                return BadRequest(ErrorKind.TASK_NOT_COMPLETE.Response());
            if (TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(), taskStatus.TaskRefreshTime,
                    timeZoneOffset.Value,
                    0) != 0)
                return BadRequest(ErrorKind.TASK_NOT_COMPLETE.Response());

            for (int i = 0; i < serverConfigService.GetDailyTaskList().Count; i++)
            {
                if ((taskStatus.DailyTaskRewardClaimStatus & (1 << i)) > 0)
                    continue;
                if (taskStatus.TaskProgress[i] >= serverConfigService.GetDailyTaskList()[i].target_progress)
                    taskStatus.DailyTaskRewardClaimStatus |= (1 << i);
            }

            await context.SaveChangesWithDefaultRetryAsync();
            return Ok(taskStatus.DailyTaskRewardClaimStatus);
        });
    }

    [HttpPost]
    public async Task<ActionResult<TakeRewardResult?>> ClaimActiveScoreReward(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= serverConfigService.GetActiveScoreRewardList().Count)
            return BadRequest(ErrorKind.ARG_OUT_OF_RANGE.Response());
        var taskList = serverConfigService.GetDailyTaskList();
        var rewardConfig = serverConfigService.GetActiveScoreRewardList()[levelIndex];

        return await context.WithRCUDefaultRetry<ActionResult<TakeRewardResult?>>(async _ =>
        {
            var userAsset = await userAssetService.GetUserAssetsSimpleAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            var taskStatus = await context.GetDailyTask(PlayerShard, PlayerId);
            if (taskStatus == null)
                return BadRequest(ErrorKind.TASK_NOT_COMPLETE.Response());
            if (TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(), taskStatus.TaskRefreshTime,
                    userAsset.TimeZoneOffset, 0) != 0)
                return BadRequest(ErrorKind.TASK_NOT_COMPLETE.Response());

            if ((taskStatus.ActiveScoreRewardClaimStatus & (1 << levelIndex)) > 0)
                return BadRequest(ErrorKind.REWARD_CLAIMED.Response());

            int activeScore = 0;
            for (int i = 0; i < taskStatus.TaskProgress.Count; i++)
            {
                if (taskList.Count <= i) continue;
                if (taskStatus.TaskProgress[i] >= taskList[i].target_progress)
                    activeScore += taskList[i].active_score_reward;
            }

            if (activeScore < rewardConfig.score_required)
                return BadRequest(ErrorKind.TASK_NOT_COMPLETE.Response());

            taskStatus.ActiveScoreRewardClaimStatus |= (1 << levelIndex);
            var generalReward = new GeneralReward()
            {
                ItemList = new() { rewardConfig.item_id },
                CountList = new() { rewardConfig.item_count }
            };
            var (newCardList, takeRewardResult) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            if (takeRewardResult == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());
            // 使用事务确保一致性
            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(
                newCardList, PlayerShard, PlayerId
            );
            if (takeRewardResult.AssetsChange != null)
                takeRewardResult.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();
            return Ok(takeRewardResult);
        });
    }

    public record struct ClaimH5FriendActivityReply(
        TakeRewardResult Result,
        UserH5FriendActivityInfo UserH5FriendActivityInfo);

    // ================================ 以下是废弃方法，等过期 ======================================
    [HttpPost]
    public async Task<ActionResult<ClaimH5FriendActivityReply>> ClaimH5FriendActivityReward()
    {
        // 正常活动关闭后不应该被访问到了，为了保底加的API，回头新版本开启时用新的接口
        if (TimeUtils.ShouldOldH5ActivityClose())
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());
        // 检查配置
        var rewardConfigList = serverConfigService.GetParameterIntList(Params.H5FriendRequestRewardItemAndCount);
        if (rewardConfigList == null || rewardConfigList.Count != 2)
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
        if (!serverConfigService.TryGetParameterInt(Params.H5FriendActivityUnlockLevel, out var activityUnlockLevel))
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());

        return await context.WithRCUDefaultRetry<ActionResult<ClaimH5FriendActivityReply>>(async _ =>
        {
            var (_, activityInfo) =
                await context.GetOrCreateUserH5FriendActivityInfo(PlayerId, PlayerShard, activityUnlockLevel);

            var generalReward = new GeneralReward()
            {
                ItemList = new List<int>() { rewardConfigList[0] },
                CountList = new List<int>() { rewardConfigList[1] },
            };

            var includeOption = userItemService.CalculateUserAssetIncludeOptions(generalReward.ItemList);
            var userAsset
                = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });

            // 检查当期是否领取过奖励
            if (userAsset.LevelData.Level < activityInfo.NextShowingLevelV1)
                return BadRequest(ErrorKind.REWARD_CLAIMED.Response()); //这个如果客户端状态正确，是不会有机会返回这个的

            // 发放奖励
            var (newCardList, result) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            if (result == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

            // 修正奖励领取状态, Next Showing Level 步进, 以当前为起点步进n格
            if (!serverConfigService.TryGetParameterInt(Params.H5FriendActivityShowCd, out var stepWidth))
                return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
            activityInfo.NextShowingLevelV1 = userAsset.LevelData.Level + stepWidth;

            // 使用事务确保一致性
            await using var t = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(
                newCardList, PlayerShard, PlayerId
            );
            if (result.AssetsChange != null)
                result.AssetsChange.AchievementChanges.AddRange(achievements);
            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();
            return Ok(new ClaimH5FriendActivityReply(result, activityInfo));
        });
    }
    // ================================ 以上是废弃方法，等过期 ======================================

    [HttpPost]
    public async Task<ActionResult<ClaimH5FriendActivityReply>> ClaimH5FriendActivityReward_V1()
    {
        // 检查配置
        var rewardConfigList = serverConfigService.GetParameterIntList(Params.H5FriendRequestRewardItemAndCount);
        if (rewardConfigList == null || rewardConfigList.Count != 2)
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
        if (!serverConfigService.TryGetParameterInt(Params.H5FriendActivityUnlockLevel, out var activityUnlockLevel))
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());

        return await context.WithRCUDefaultRetry<ActionResult<ClaimH5FriendActivityReply>>(async _ =>
        {
            var (_, activityInfo) =
                await context.GetOrCreateUserH5FriendActivityInfo(PlayerId, PlayerShard, activityUnlockLevel);

            var generalReward = new GeneralReward()
            {
                ItemList = new List<int>() { rewardConfigList[0] },
                CountList = new List<int>() { rewardConfigList[1] },
            };

            var includeOption = userItemService.CalculateUserAssetIncludeOptions(generalReward.ItemList);
            var userAsset
                = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });

            // 检查当期是否领取过奖励
            if (userAsset.LevelData.Level < activityInfo.NextShowingLevelV1)
                return BadRequest(ErrorKind.REWARD_CLAIMED.Response()); //这个如果客户端状态正确，是不会有机会返回这个的
            // 1. 发放奖励
            var (newCardList, result) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            if (result == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

            // 2. 修正奖励领取状态, Next Showing Level 步进, 以当前为起点步进n格
            if (!serverConfigService.TryGetParameterInt(Params.H5FriendActivityShowCd, out var stepWidth))
                return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
            activityInfo.NextShowingLevelV1 = userAsset.LevelData.Level + stepWidth;
            // 3. 调用Reset
            try
            {
                await assistActivityManagementService.Reset(new ResetInviteCodeRequest()
                {
                    PlayerId = PlayerId,
                    DistroId = DistroId,
                    PlayerShard = PlayerShard
                });
            }
            catch (Exception e)
            {
                return BadRequest(ErrorKind.CACHE_UNKNOWN_ERROR.Response());
            }

            // ========================= 使用事务封装 ========================= 
            await using var t = await context.Database.BeginTransactionAsync();
            // 4. Save一下Changes
            await context.SaveChangesWithDefaultRetryAsync(false);
            // 5. 修改成就，成就有Upsert
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(
                newCardList, PlayerShard, PlayerId
            );
            if (result.AssetsChange != null)
                result.AssetsChange.AchievementChanges.AddRange(achievements);

            await t.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();
            return Ok(new ClaimH5FriendActivityReply(result, activityInfo));
        });
    }

    public record struct ClaimH5InvitationCodeRewardReply(
        TakeRewardResult? Result,
        UserH5FriendActivityInfo UserH5FriendActivityInfo);

    // ================================ 以下是废弃方法，等过期 ======================================
    [HttpPost]
    public async Task<ActionResult<ClaimH5InvitationCodeRewardReply>> ClaimH5InvitationCodeReward(string invitationCode)
    {
        // 正常活动关闭后不应该被访问到了，为了保底加的API，回头新版本开启时用新的接口
        if (TimeUtils.ShouldOldH5ActivityClose())
            return BadRequest(ErrorKind.ACTIVITY_NOT_OPEN.Response());

        invitationCode = invitationCode.Trim();
        // 判断自己是不是领取过邀请码
        if (!serverConfigService.TryGetParameterInt(Params.H5FriendActivityUnlockLevel, out var activityUnlockLevel))
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());

        // 领取邀请码奖励
        return await context.WithRCUDefaultRetry<ActionResult<ClaimH5InvitationCodeRewardReply>>(async _ =>
        {
            var (_, activityInfo) =
                await context.GetOrCreateUserH5FriendActivityInfo(PlayerId, PlayerShard, activityUnlockLevel);
            if (activityInfo.ClaimedInvitationCode)
                return BadRequest(ErrorKind.REWARD_CLAIMED.Response());
            PlayerActivityInfo playerActivityInfo = null;
            try
            {
                playerActivityInfo = await assistActivityManagementService.Validate(
                        new ValidateActivityRequest() { InviteCode = invitationCode, });
            }
            catch (Exception e)
            {
                // ignored
                // 异常包一下，有可能抛出异常
            }
            // 判断邀请码是不是有效
            if (playerActivityInfo == null)
                return BadRequest(ErrorKind.WRONG_PARAM.Response());
            // 判断邀请码是不是自己的
            if (playerActivityInfo.PlayerId == PlayerId)
                return BadRequest(ErrorKind.H5_CLAIMING_SELF_REWARD.Response());

            var (newCardList, claimResult, error) =
                await invitationCodeService.ClaimInvitationCode(invitationCode, PlayerId, PlayerShard, GameVersion);
            activityInfo.ClaimedInvitationCode = true;
            if (error != ErrorKind.SUCCESS)
                return BadRequest(error.Response());
            // 保存，返回
            await using var transaction = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(
                newCardList, PlayerShard, PlayerId
            );
            if (claimResult is { AssetsChange: not null })
                claimResult.AssetsChange.AchievementChanges.AddRange(achievements);
            await transaction.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();
            return Ok(new ClaimH5InvitationCodeRewardReply(claimResult, activityInfo));
        });
    }
    // ================================ 以上是废弃方法，等过期 ======================================

    /// <summary>
    /// 这个接口跨的服务太多，还是不封装了，不然也够恶心的
    /// </summary>
    /// <param name="invitationCode"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<ClaimH5InvitationCodeRewardReply>> ClaimH5InvitationCodeReward_V1(
        string invitationCode)
    {
        invitationCode = invitationCode.Trim();
        // 配置检查
        var rewardItemList = serverConfigService.GetParameterIntList(Params.H5FriendActivityInvitationCodeItemList);
        if (rewardItemList == null)
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
        var rewardCountList = serverConfigService.GetParameterIntList(Params.H5FriendActivityInvitationCodeItemCount);
        if (rewardCountList == null)
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
        // 判断自己是不是领取过邀请码
        if (!serverConfigService.TryGetParameterInt(Params.H5FriendActivityUnlockLevel, out var activityUnlockLevel))
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());
        var (_, activityInfo) =
                await context.GetOrCreateUserH5FriendActivityInfo(PlayerId, PlayerShard, activityUnlockLevel);
        if (activityInfo.ClaimedInvitationCode)
            return BadRequest(ErrorKind.REWARD_CLAIMED.Response());
        // 邀请码验证
        PlayerActivityInfo targetPlayerActivityInfo = null;
        try
        {
            targetPlayerActivityInfo = await assistActivityManagementService.Validate(
                new ValidateActivityRequest() { InviteCode = invitationCode, });
        }
        catch (Exception e)
        {
            // ignored
        }

        // 判断邀请码是不是有效
        if (targetPlayerActivityInfo == null)
            return BadRequest(ErrorKind.WRONG_PARAM.Response());
        // 判断邀请码是不是自己的
        if (targetPlayerActivityInfo.PlayerId == PlayerId)
            return BadRequest(ErrorKind.H5_CLAIMING_SELF_REWARD.Response());

        // 领取邀请码奖励
        return await context.WithRCUDefaultRetry<ActionResult<ClaimH5InvitationCodeRewardReply>>(async _ =>
        {
            // 1. 礼包码领取 + 计数
            PlayerActivityInfo playerActivityInfo = null;
            try
            {
                playerActivityInfo = await assistActivityManagementService.RedeemInviteCode(
                    new RedeemInviteCodeRequest()
                    {
                        PlayerId = PlayerId,
                        InviteCode = invitationCode,
                        PlayerShard = PlayerShard,
                        DistroId = DistroId
                    });
            }
            catch (Exception e)
            {
                // ignore
            }
            if (playerActivityInfo == null)
                return BadRequest(ErrorKind.CACHE_UNKNOWN_ERROR.Response());

            var inviteInfo = playerActivityInfo.InviteCodeRedeemedPlayerIds
                .FirstOrDefault(x => x.Round == playerActivityInfo.Round);
            if (inviteInfo is null || !inviteInfo.PlayerId.Contains(PlayerId))
            {
                return BadRequest(ErrorKind.REWARD_CLAIMED.Response());
            }
            // 2. 标记这个人已经领过礼包码了
            activityInfo.ClaimedInvitationCode = true;
            // 3. 发放奖励
            var generalReward = new GeneralReward();
            generalReward.ItemList = rewardItemList;
            generalReward.CountList = rewardCountList;
            // 4. 发放奖励
            var includeOption = userItemService.CalculateUserAssetIncludeOptions(generalReward.ItemList);
            var userAsset = await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET.Response());
            var (newCardList, claimResult) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            if (claimResult == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG.Response());

            await using var transaction = await context.Database.BeginTransactionAsync();
            // 5. 保存
            await context.SaveChangesWithDefaultRetryAsync(false);
            // 6. 发放成就
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(
                newCardList, PlayerShard, PlayerId);
            claimResult.AssetsChange.AchievementChanges.AddRange(achievements);
            await transaction.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();
            return Ok(new ClaimH5InvitationCodeRewardReply(claimResult, activityInfo));
        });
    }

    public record struct ClaimIosGameCenterRewardReply(TakeRewardResult? Result);

    [HttpPost]
    // 业务层实在是太小了，就不封装service 和 repository了, 下不去手
    public async Task<ActionResult<ClaimIosGameCenterRewardReply>> ClaimIosGameCenterReward()
    {
        var rewardList = serverConfigService.GetParameterIntList(Params.IosGameCenterRewards);
        if (rewardList == null || rewardList.Count != 2)
            return BadRequest(ErrorKind.NO_PARAM_CONFIG.Response());

        return await context.WithRCUDefaultRetry<ActionResult<ClaimIosGameCenterRewardReply>>(async _ =>
        {
            var iosGameCenterRewardInfo = await context.IosGameCenterRewardInfos.FindAsync(PlayerId, PlayerShard);
            if (iosGameCenterRewardInfo != null)
                return Ok(new ClaimIosGameCenterRewardReply(null));
            // 发放奖励
            var generalReward = new GeneralReward();
            generalReward.ItemList = new List<int>() { rewardList[0] };
            generalReward.CountList = new List<int>() { rewardList[1] };
            var includeOption = userItemService.CalculateUserAssetIncludeOptions(generalReward.ItemList);

            var userAsset =
                await userAssetService.GetUserAssetsByIncludeOptionAsync(PlayerShard, PlayerId, includeOption);
            if (userAsset == null)
                return BadRequest(ErrorKind.NO_USER_ASSET);
            var (newCardList, result) = await userItemService.TakeReward(userAsset, generalReward, GameVersion);
            if (result == null)
                return BadRequest(ErrorKind.NO_ITEM_CONFIG);
            // 标记领取
            context.IosGameCenterRewardInfos.Add(new IosGameCenterRewardInfo()
            {
                PlayerId = PlayerId,
                ShardId = PlayerShard,
                RewardClaimed = true
            });
            // 保存
            await using var transaction = await context.Database.BeginTransactionAsync();
            await context.SaveChangesWithDefaultRetryAsync(false);
            var achievements = await userAchievementService.UpdateCardUpgradeAchievementProgressAsync(
                newCardList, PlayerShard, PlayerId
            );
            if (result.AssetsChange is not null)
                result.AssetsChange.AchievementChanges.AddRange(achievements);
            await transaction.CommitAsync();
            context.ChangeTracker.AcceptAllChanges();
            return Ok(new ClaimIosGameCenterRewardReply(result));
        });
    }

    [HttpPost]
    public async Task<ActionResult<bool>> IsGameCenterRewardClaimed()
    {
        var hasReward =
            await context.IosGameCenterRewardInfos.AnyAsync(ri => ri.PlayerId == PlayerId && ri.ShardId == PlayerShard);
        return Ok(hasReward);
    }
}