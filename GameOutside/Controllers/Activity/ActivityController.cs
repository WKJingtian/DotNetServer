﻿using ChillyRoom.Infra.ApiController;
using GameOutside;
using GameOutside.DBContext;
using GameOutside.Util;
using Microsoft.AspNetCore.Mvc;
using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Functions.DBModel;
using GameOutside.Services;
using Microsoft.AspNetCore.Authorization;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;

[Authorize]
public partial class ActivityController(
    IConfiguration configuration,
    ILogger<ActivityController> logger,
    ActivityService activityService,
    UserItemService userItemService,
    UserAssetService userAssetService,
    ServerDataService serverDataService,
    BuildingGameDB dbContext,
    ServerConfigService serverConfig,
    UserAchievementService userAchievementService) : BaseApiController(configuration)
{
    public record struct OpeningActivityData(
        LuckyStarDataClient? LuckyStar,
        FortuneBagDataClient? FortuneBag,
        UnrivaledGodDataClient? UnrivaledGod,
        CoopBossDataClient? CoopBoss,
        TreasureMazeDataClient? TreasureMaze,
        EndlessChallengeDataClient? EndlessChallenge,
        FortuneBagClaimReply? ExpiredFortuneBagClaimReply,
        SlotMachineDataClient? SlotMachine,
        OneShotKillDataClient? OneShotKill,
        TakeRewardResult? ExpiredSlotMachineRewardClaimReply,
        RpgGameDataClient? RpgGame,
        LoogGameDataClient? LoogGame,
        TreasureHuntDataClient? TreasureHunt,
        TakeRewardResult? ExpiredTreasureHuntKeyRefundReply,
        CsgoStyleLotteryDataClient? CsgoStyleLottery,
        TakeRewardResult? CsgoLotteryPassRewardReply,
        TakeRewardResult? ExpiredCsgoLotteryReply);

    /// <param name="dayChanged">废弃字段，但为了保证前后端API一致，需要保留</param>
    [HttpPost]
    public async Task<ActionResult<OpeningActivityData>> GetOpeningActivityData(bool dayChanged)
    {
        return await dbContext.WithRCUDefaultRetry<ActionResult<OpeningActivityData>>(async _ =>
        {
            var result = new OpeningActivityData();
            // 获取完整用户数据，用于处理过期活动奖励发放
            var userAsset = await userAssetService.GetUserAssetsDetailedAsync(PlayerShard, PlayerId);
            if (userAsset == null)
                return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_ASSET });

            bool databaseChanged = false;
            var openingActivity = activityService.GetOpeningActivities(GameVersion);
            foreach (var activity in openingActivity)
            {
                switch (activity.activity_type)
                {
                    case ActivityType.ActivityLuckyStar:
                    {
                        // 福星活动
                        var luckyStarData = await activityService.GetActivityLuckStarDataAsync(PlayerId, PlayerShard, TrackingOptions.NoTracking);
                        if (luckyStarData != null && luckyStarData.ActivityId != activity.id)
                            luckyStarData.Reset(activity.id);

                        if (luckyStarData == null)
                            luckyStarData = DataUtil.DefaultLuckyStarData(PlayerId, PlayerShard, activity.id);
                        result.LuckyStar = luckyStarData.ToClientApi();
                        break;
                    }
                    case ActivityType.ActivityFortuneBag:
                    {
                        // 福袋
                        result.FortuneBag
                            = await activityService.FetchFortuneBagActivityInfoAsync(activity.id, PlayerId, PlayerShard);
                        break;
                    }
                    case ActivityType.ActivityUnrivaledGod:
                    {
                        // 无双神将
                        var unrivaledGodData = await activityService.GetUnrivaledGodDataAsync(PlayerId, PlayerShard,
                            activity.id, TrackingOptions.NoTracking);
                        if (unrivaledGodData == null)
                        {
                            // 初始化
                            unrivaledGodData
                                = await activityService.CreateDefaultUnrivaledGodDataAsync(PlayerId, PlayerShard,
                                    activity.id);
                            databaseChanged = true;
                        }
                        if (userAsset == null)
                            break;
                        // 完成登陆任务，增加任务进度的接口会同时判断一下任务是不是跨天了
                        databaseChanged |= activityService.RecordUnrivaledGodTask(unrivaledGodData,
                            ActivityTaskKeys.Login, 1, userAsset.TimeZoneOffset);
                        databaseChanged |= activityService.RecordUnrivaledGodTask(unrivaledGodData,
                            ActivityTaskKeys.LoginDaily, 1, userAsset.TimeZoneOffset);
                        result.UnrivaledGod = unrivaledGodData.ToClientApi();
                        break;
                    }
                    case ActivityType.ActivityCoopBoss:
                    {
                        // 联机好友共斗boss活动
                        var coopBossData = await activityService.GetCoopBossDataAsync(PlayerId, PlayerShard,
                            activity.id, TrackingOptions.NoTracking);
                        if (coopBossData == null)
                            coopBossData = DataUtil.DefaultCoopBossData(PlayerId, PlayerShard, activity.id);
                        else
                        {
                            if (userAsset == null)
                                break;
                            // 检查一下跨天刷新
                            var newDay = TimeUtils.GetDayDiffBetween(TimeUtils.GetCurrentTime(), coopBossData.LastRefreshTime,
                                userAsset.TimeZoneOffset, 0) > 0;
                            if (newDay)
                            {
                                coopBossData.LastRefreshTime = TimeUtils.GetCurrentTime();
                                coopBossData.GameEndCountToday = 0;
                                coopBossData.RefreshCountToday = 0;
                                databaseChanged = true;
                            }
                        }

                        result.CoopBoss = coopBossData.ToClientApi();
                        break;
                    }
                    case ActivityType.ActivityTreasureMaze:
                    {
                        // 联机宝藏迷宫活动
                        var treasureBossData
                            = await activityService.GetTreasureMazeDataAsync(PlayerId, PlayerShard, activity.id);
                        var startTimestamp = TimeUtils.ParseDateTimeStrToUnixSecond(activity.start_time)
                                             - 86400; // 得减去一天的时间，这样活动第一天就会刷新一次钥匙
                        if (treasureBossData == null)
                            treasureBossData = DataUtil.DefaultTreasureMazeData(PlayerId, PlayerShard, activity.id, startTimestamp);
                        // 每日刷新
                        var (updateSuccess, treasureMazeDataChanged)
                            = activityService.CheckRefreshTreasureMazeData(treasureBossData, userAsset.TimeZoneOffset);
                        if (!updateSuccess)
                            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_PARAM_CONFIG });
                        databaseChanged |= treasureMazeDataChanged;
                        result.TreasureMaze = treasureBossData.ToClientApi();
                        break;
                    }
                    case ActivityType.ActivityEndlessChallenge:
                    {
                        // 轮回挑战
                        var endlessData
                            = await activityService.GetEndlessChallengeDataAsync(PlayerId, PlayerShard, activity.id);
                        if (endlessData == null)
                            endlessData = DataUtil.DefaultEndlessChallengeData(PlayerId, PlayerShard, activity.id);
                        var currentTime = TimeUtils.GetCurrentTime();
                        if (userAsset == null)
                            break;
                        // 检查挑战次数是否需要重置
                        if (TimeUtils.GetDayDiffBetween(currentTime, endlessData.LastGameTime, userAsset.TimeZoneOffset,
                                0) > 0)
                        {
                            endlessData.TodayGameCount = 0;
                        }

                        result.EndlessChallenge = endlessData.ToClientApi();
                        break;
                    }
                    case ActivityType.ActivitySlotMachine:
                    {
                        var slotMachineData = await activityService.GetSlotMachineDataAsync(PlayerId, PlayerShard, activity.id, TrackingOptions.NoTracking);
                        if (slotMachineData == null)
                            slotMachineData = DataUtil.DefaultSlotMachineData(PlayerId, PlayerShard, activity.id);
                        var currentTime = TimeUtils.GetCurrentTime();
                        result.SlotMachine = slotMachineData.ToClientApi();
                        if (TimeUtils.GetDayDiffBetween(currentTime, slotMachineData.LastDrawTime, userAsset.TimeZoneOffset,
                                0) > 0 &&
                            slotMachineData.RewardsInSlot.Count == 0)
                            result.SlotMachine.DrawTimeToday = 0;
                        break;
                    }
                    case ActivityType.ActivityOneShotKill:
                    {
                        var oneShotKillData = await activityService.GetOneShotKillDataAsync(PlayerId, PlayerShard, activity.id, TrackingOptions.NoTracking);
                        if (oneShotKillData == null)
                            oneShotKillData = DataUtil.DefaultOneShotKillData(PlayerId, PlayerShard, activity.id);
                        var currentTime = TimeUtils.GetCurrentTime();

                        var mapProgress = await activityService.GetOneShotKillMapProgressAsync(activity.id);
                        var taskProgress = await activityService.GetOneShotKillTaskProgressAsync(activity.id);
                        var progressAdd = await activityService.GetOneShotKillLocalProgressAddAsync(activity.id);
                        var globalProgressAdd = activityService.GetOneShotKillGlobalProgressAdd(activity.id, taskProgress);
                        progressAdd = progressAdd.Select(i => i + globalProgressAdd).ToList();
                        var progressLose = await activityService.GetOneShotKillLocalProgressMinusAsync(activity.id);
                        var eventList = await activityService.GetOneShotKillEventListAsync(activity.id);
                        result.OneShotKill = oneShotKillData.ToClientApi(mapProgress, taskProgress, progressAdd, progressLose, eventList);

                        if (TimeUtils.GetDayDiffBetween(currentTime, oneShotKillData.NormalVictoryUpdateTimestamp,
                                userAsset.TimeZoneOffset,
                                0) > 0)
                            result.OneShotKill.NormalVictoryToday = 0;
                        if (TimeUtils.GetDayDiffBetween(currentTime, oneShotKillData.ChallengeVictoryUpdateTimestamp,
                                userAsset.TimeZoneOffset,
                                0) > 0)
                            result.OneShotKill.ChallengeVictoryToday = 0;
                        (await activityService.GetOneShotKillEventListAsync(activity.id)).Select(
                            item => new OneShotKillEvent()
                            {
                                EventHash = (int)item.Name,
                                Timestamp = (long)item.Value,
                            }).ToList();
                        break;
                    }
                    case ActivityType.ActivityRpgGame:
                    {
                        var rpgGameData = await activityService.GetRpgGameDataAsync(PlayerId, PlayerShard, activity.id, TrackingOptions.NoTracking);
                        if (rpgGameData == null)
                            rpgGameData = DataUtil.DefaultRpgGameData(PlayerId, PlayerShard, activity.id);
                        var currentTime = TimeUtils.GetCurrentTime();
                        if (TimeUtils.GetDayDiffBetween(currentTime, rpgGameData.LastGameCountRecordTime,
                                userAsset.TimeZoneOffset,
                                0) > 0)
                            rpgGameData.TodayGameCount = 0;
                        result.RpgGame = rpgGameData.ToClientApi();
                        break;
                    }
                    case ActivityType.ActivityLoogGame:
                    {
                        var loogGameData = await activityService.GetLoogGameDataAsync(PlayerId, PlayerShard, activity.id, TrackingOptions.NoTracking);
                        if (loogGameData == null)
                            loogGameData = DataUtil.DefaultLoogGameData(PlayerId, PlayerShard, activity.id);
                        var currentTime = TimeUtils.GetCurrentTime();
                        if (TimeUtils.GetDayDiffBetween(currentTime, loogGameData.LastGameCountRecordTime,
                                userAsset.TimeZoneOffset,
                                0) > 0)
                            loogGameData.TodayGameCount = 0;
                        result.LoogGame = loogGameData.ToClientApi();
                        break;
                    }
                    case ActivityType.ActivityTreasureHunt:
                    {
                        // 灵犀探宝
                        (bool changed, TreasureHuntDataClient data)
                            = await GetTreasureHuntDataAsync(activity, userAsset);
                        databaseChanged |= changed;
                        result.TreasureHunt = data;
                        break;
                    }
                    case ActivityType.ActivityCsgoStyleLottery:
                    {
                        // Csgo风格抽奖活动
                        var lotteryData = await activityService.GetCsgoStyleLotteryDataAsync(PlayerId, PlayerShard, activity.id, TrackingOptions.Tracking);
                        if (lotteryData == null)
                        {
                            lotteryData = activityService.CreateDefaultCsgoStyleLotteryData(PlayerId, PlayerShard, activity.id);
                            databaseChanged = true;
                        }
                        // 刷新任务进度（日常任务跨天重置）
                        databaseChanged |= activityService.CheckRefreshCsgoStyleLotteryTask(lotteryData, userAsset.TimeZoneOffset);
                        // 记录登录任务
                        databaseChanged |= activityService.RecordCsgoStyleLotteryTask(lotteryData, ActivityTaskKeys.Login, 1, userAsset.TimeZoneOffset);
                        databaseChanged |= activityService.RecordCsgoStyleLotteryTask(lotteryData, ActivityTaskKeys.LoginDaily, 1, userAsset.TimeZoneOffset);
                        // 发放csgo lottery pass 的每日奖励
                        var (passRewardChanged, passRewardResult) = await ClaimCsgoLotteryPassDailyRewards(lotteryData, activity, userAsset);
                        databaseChanged |= passRewardChanged;
                        result.CsgoLotteryPassRewardReply = passRewardResult;
                        result.CsgoStyleLottery = lotteryData.ToClientApi(userAsset);
                        break;
                    }
                }
            }

            var newFortuneBagActivityId = result.FortuneBag?.ActivityId ?? -1;
            var (dbChangedByFortuneBag, expiredFortuneBagReply) =
                await ClaimExpiredFortuneBag(newFortuneBagActivityId, userAsset);
            databaseChanged |= dbChangedByFortuneBag;
            result.ExpiredFortuneBagClaimReply = expiredFortuneBagReply;

            var newSlotMachineActivityId = result.SlotMachine?.ActivityId ?? -1;
            var (dbChangedBySlotMachine, expiredSlotMachineRewardReply) =
                await TakeExpiredSlotMachineReward(newSlotMachineActivityId);
            databaseChanged |= dbChangedBySlotMachine;
            result.ExpiredSlotMachineRewardClaimReply = expiredSlotMachineRewardReply;
            
            var (dbChangedByTreasureHunt, expiredTreasureHuntReply) =
                await HandleExpiredTreasureHuntAsync(userAsset);
            databaseChanged |= dbChangedByTreasureHunt;
            result.ExpiredTreasureHuntKeyRefundReply = expiredTreasureHuntReply;
            
            var (dbChangedByCsgoLottery, expiredCsgoLotteryReply) =
                await HandleExpiredCsgoLottery(userAsset);
            databaseChanged |= dbChangedByCsgoLottery;
            result.ExpiredCsgoLotteryReply = expiredCsgoLotteryReply;

            if (databaseChanged)
            {
                // 使用事务确保一致性
                await using var t = await dbContext.Database.BeginTransactionAsync();
                await dbContext.SaveChangesWithDefaultRetryAsync(false);
                await t.CommitAsync();
                dbContext.ChangeTracker.AcceptAllChanges();
            }

            return Ok(result);
        });
    }
}