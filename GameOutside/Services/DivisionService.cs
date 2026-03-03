using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.MQ;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Repositories;
using GameOutside.Services.KafkaProducers;
using GameOutside.Util;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Services;

public class DivisionService(
    ServerConfigService serverConfigService,
    LeaderboardModule leaderboardModule,
    BuildingGameDB dbCtx,
    UserRankService userRankService,
    UserEndlessRankService userEndlessRankService,
    SeasonService seasonService,
    IUserRankGroupRepository userRankGroupRepository,
    IUserRankRepository userRankRepository,
    ISeasonRefreshedHistoryRepository seasonRefreshedHistoryRepository,
    IUserDivisionRepository userDivisionRepository,
    UserInfoService userInfoService,
    ILogger<DivisionService> logger,
    RefreshUserDivisionProducer refreshUserDivisionProducer,
    RefreshWorldRankProducer refreshWorldRankProducer)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<int> GetDivisionNumberAsync(short shardId, long playerId, CreateOptions createOptions)
    {
        return serverConfigService.GetDivisionByDivisionScore(await GetUserDivisionScoreAsync(shardId, playerId, createOptions) ?? 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<int?> GetUserDivisionScoreAsync(short shardId, long playerId, CreateOptions createOptions)
    {
        return await userDivisionRepository.GetUserDivisonScoreAsync(shardId, playerId, createOptions);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<UserDivision?> GetUserDivisionAsync(short shardId, long playerId, TrackingOptions trackingOptions)
    {
        return dbCtx.WithDefaultRetry(_ => userDivisionRepository.GetUserDivisionAsync(shardId, playerId, trackingOptions));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UserDivision CreateUserDivision(short shardId, long playerId, int divisionScore)
    {
        return userDivisionRepository.CreateUserDivision(shardId, playerId, divisionScore);
    }

    private const int BronzeDivisionNumber = 0;
    // TODO: 并发执行该函数和拉取/更新 UserRanks/UserEndlessRanks/UserDivisions 进行测试
    /// <summary>
    /// 刷新青铜段位
    /// </summary>
    /// <param name="userDivision">已被 EF Core 跟踪的 UserDivision 实体</param>
    /// <returns>是否执行了刷新</returns>
    public async Task<bool> RefreshBronzeDivisionAsync(UserDivision userDivision, short shardId, long playerId)
    {
        var fixedLevelMapProgresses = await dbCtx.GetAllUserFixedLevelMapProgresses(playerId, shardId);
        int gameCount = 0;
        foreach (var status in fixedLevelMapProgresses)
        {
            var mapConfig = serverConfigService.GetFixedMapConfig(status.MapId);
            if (mapConfig != null && mapConfig.gameplay_type != GameplayType.Train && status.StarCount > 0)
                gameCount++;
        }

        var bronzeConfig = serverConfigService.GetDivisionConfig(0);
        if (gameCount < bronzeConfig.game_count_to_next_division)
        {
            return false;
        }

        // 青铜段位的赛季号固定
        int seasonNumber = SeasonService.BronzeSeasonNumber;
        userDivision.LastDivisionScore = userDivision.DivisionScore;
        var scoreChange = serverConfigService.GetRewardsByRank(userDivision.DivisionScore, 0, int.MaxValue);
        userDivision.MaxDivisionScore = Math.Max(userDivision.DivisionScore, userDivision.MaxDivisionScore);
        userDivision.DivisionScore += scoreChange;
        userDivision.LastDivisionRank = 0;
        userDivision.LastWorldRank = -1;
        userDivision.LastSeasonNumber = seasonNumber;
        userDivision.RewardReceived = false;

        await userRankService.RemoveUserRankAsync(shardId, playerId, seasonNumber);
        await leaderboardModule.RemovePlayerFromLeaderBoard(playerId, LeaderboardModule.NormalModeLeaderBoardId,
            seasonNumber);
        return true;
    }

    /// <summary>
    /// 赛季更迭，对上个赛季刷新世界排名信息
    /// </summary>
    /// <param name="seasonToBeRefreshed"></param>
    public async Task RefreshWorldRankAsync(int seasonToBeRefreshed)
    {
        CheckSeasonsToBeRefreshed(seasonToBeRefreshed);
        var top100Players
            = await leaderboardModule.GetTopPlayers(0, 100, LeaderboardModule.NormalModeLeaderBoardId, seasonToBeRefreshed);

        var sendTasks = new List<Task>(top100Players.Length);

        for (var rank = 0; rank < top100Players.Length; ++rank)
        {
            var player = top100Players[rank];
            if (player.PlayerId == -1)
                continue;

            var sendTask = refreshWorldRankProducer.ProduceAsync(
                new RetryableKafkaMessage(new RefreshWorldRankMessage()
                {
                    PlayerId = player.PlayerId,
                    SeasonToBeRefreshed = seasonToBeRefreshed,
                    WorldRank = rank,
                }), null);

            sendTasks.Add(sendTask);
        }

        try
        {
            await Task.WhenAll(sendTasks);
        }
        catch
        {
            var failedTasks = sendTasks.Where(t => t.IsFaulted).ToList();
            foreach (var failedTask in failedTasks)
            {
                logger.LogError(failedTask.Exception, "Failed to send refresh world rank message for season {Season}",
                    seasonToBeRefreshed);
            }

            logger.LogError(
                "Failed to send {FailedCount} out of {TotalCount} refresh division messages for season {Season}",
                failedTasks.Count, sendTasks.Count, seasonToBeRefreshed);

            throw;
        }
    }

    /// <summary>
    /// 赛季更迭，对上个赛季刷新非 division-0 段位分数和奖励
    /// </summary>
    public async Task RefreshDivisionAsync(int seasonToBeRefreshed)
    {
        CheckSeasonsToBeRefreshed(seasonToBeRefreshed);

        // 以下步骤需要按顺序完成，因此需要多次保存
        // 刷新段位排名和分数
        for (int division = BronzeDivisionNumber + 1; division < serverConfigService.TotalDivisionCount; division++)
        {
            await RefreshRewardsByLevelAsync(seasonToBeRefreshed, division);
        }

        // 增加赛季结算记录
        seasonRefreshedHistoryRepository.AddHistory(new SeasonRefreshedHistory
        {
            SeasonNumber = seasonToBeRefreshed,
            RefreshedTime = DateTime.UtcNow,
        });
        try
        {
            await dbCtx.SaveChangesWithDefaultRetryAsync();
        }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException
        {
            SqlState: Npgsql.PostgresErrorCodes.UniqueViolation
        })
        {
            // 赛季结算记录已存在，忽略异常
            logger.LogInformation("Season {SeasonNumber} already refreshed, ignoring exception: {ExceptionMessage}", seasonToBeRefreshed, e.Message);
        }
    }

    public async Task CleanUpExpiredDataAsync(int seasonToBeRefreshed)
    {
        CheckSeasonsToBeRefreshed(seasonToBeRefreshed);
        await ClearExpiredDataAsync(seasonToBeRefreshed);
    }

    private void CheckSeasonsToBeRefreshed(int seasonToBeRefreshed)
    {
        var currentSeason = seasonService.GetCurrentSeasonNumber();
        // 当前是第一个赛季，或者期望刷新的赛季号大于等于当前赛季号，不允许刷新，抛出异常
        if (seasonToBeRefreshed < 0 || seasonToBeRefreshed >= currentSeason)
        {
            throw new InvalidOperationException(
                $"Invalid season number {seasonToBeRefreshed} to be refreshed, current season: {currentSeason}");
        }
    }

    private async Task ClearExpiredDataAsync(int seasonToBeRefreshed)
    {
        var seasonNumbersToBeKept = seasonService.GetSeasonNumbersToBeKept(seasonToBeRefreshed);
        logger.LogInformation("Begin clearing expired data, keeping seasons: {KeptSeasons}",
            string.Join(", ", seasonNumbersToBeKept));

        // 清空排行表过期赛季所有记录
        await userRankService.ClearUserRanksAsync(Consts.LocalShardId, seasonNumbersToBeKept);
        // 清空无尽模式排行表过期赛季所有记录
        await userEndlessRankService.ClearUserEndlessRankAsync(Consts.LocalShardId, seasonNumbersToBeKept);

        // 清空过期赛季的 leaderboards
        await leaderboardModule.ClearLeaderBoard(LeaderboardModule.TowerDefenceModeLeaderBoardId, seasonNumbersToBeKept);
        await leaderboardModule.ClearLeaderBoard(LeaderboardModule.TrueEndlessModeLeaderBoardId, seasonNumbersToBeKept);
        await leaderboardModule.ClearLeaderBoard(LeaderboardModule.NormalModeLeaderBoardId, seasonNumbersToBeKept);
        await leaderboardModule.ClearLeaderBoard(LeaderboardModule.SurvivorModeLeaderBoardId, seasonNumbersToBeKept);

        // 清空过期赛季的分组 ID
        await userRankGroupRepository.ClearLocalGroupIdsAsync(seasonNumbersToBeKept);
    }

    /// <summary>
    /// 根据段位排行刷新奖励
    /// </summary>
    private async Task RefreshRewardsByLevelAsync(int seasonToBeRefreshed, int division)
    {
        var allGroupIds = (await userRankGroupRepository.GetAllLocalGroupIdsAsync(seasonToBeRefreshed, division)).ToList();
        var groupBatchSize = GetGroupBatchSize(division);

        for (int i = 0; i < allGroupIds.Count; i += groupBatchSize)
        {
            var batchGroupIds = allGroupIds.Skip(i).Take(groupBatchSize).Select(id => (long)id).ToList();
            var userRanksByGroup = await dbCtx.WithDefaultRetry(_ =>
                userRankRepository.BatchGetUserRankGroupsBySeasonAsync(Consts.LocalShardId, division, batchGroupIds,
                    seasonToBeRefreshed, TrackingOptions.NoTracking));

            foreach (var groupId in batchGroupIds)
            {
                if (!userRanksByGroup.TryGetValue((int)groupId, out var userRanks) || userRanks.Count == 0)
                    continue;

                // 添加机器人排名
                userRanks.AddRange(CreateRobotUserRanks(division, (int)groupId));

                // 按分数和时间戳排序
                userRanks.Sort((UserRank a, UserRank b) =>
                {
                    if (a.HighestScore != b.HighestScore)
                        return a.HighestScore < b.HighestScore ? 1 : -1;
                    else if (a.Timestamp != b.Timestamp)
                        return a.Timestamp > b.Timestamp ? 1 : -1;
                    else
                        return 0;
                });

                int usersCount = userRanks.Count;

                var sendTasks = new List<Task>(usersCount);
                for (var rank = 0; rank < usersCount; ++rank)
                {
                    var userRankInfo = userRanks[rank];
                    if (userRankInfo.PlayerId == -1)
                        continue;

                    var sendTask = refreshUserDivisionProducer.ProduceAsync(new RetryableKafkaMessage(new RefreshUserDivisionMessage
                    {
                        ShardId = userRankInfo.ShardId,
                        PlayerId = userRankInfo.PlayerId,
                        SeasonToBeRefreshed = seasonToBeRefreshed,
                        Rank = rank,
                        UsersCount = usersCount,
                    }), null);

                    sendTasks.Add(sendTask);
                }

                try
                {
                    await Task.WhenAll(sendTasks);
                }
                catch
                {
                    var failedTasks = sendTasks.Where(t => t.IsFaulted).ToList();
                    foreach (var failedTask in failedTasks)
                    {
                        logger.LogError(failedTask.Exception, "Failed to send refresh division message for season {Season}, division {Division}, group {GroupId}",
                            seasonToBeRefreshed, division, groupId);
                    }
                    logger.LogError("Failed to send {FailedCount} out of {TotalCount} refresh division messages for season {Season}, division {Division}, group {GroupId}",
                        failedTasks.Count, sendTasks.Count, seasonToBeRefreshed, division, groupId);

                    throw;
                }
            }
        }
    }

    /// <summary>
    /// 创建机器人排名列表
    /// </summary>
    private List<UserRank> CreateRobotUserRanks(int division, int groupId)
    {
        var divisionConf = serverConfigService.GetDivisionConfig(division);
        var prngForScore = new Random(division * 1000000 + groupId);
        int robotCount = divisionConf.max_population - divisionConf.population;
        var robotList = new List<UserRank>(robotCount);
        for (int i = 0; i < robotCount; i++)
        {
            long scoreToAdd = prngForScore.Next(divisionConf.robot_score_min, divisionConf.robot_score_max);
            robotList.Add(new UserRank() { ShardId = 0, PlayerId = -1, HighestScore = scoreToAdd, Timestamp = 0 });
        }
        return robotList;
    }

    const double BatchRowCount = 2000;
    const int DefaultBatchGroupsCount = 100;

    /// <summary>
    /// 根据段位获取批量查询 groups 的数量
    /// </summary>
    private int GetGroupBatchSize(int division)
    {
        var divisionConf = serverConfigService.GetDivisionConfig(division);
        int groupsCount = divisionConf.population <= 0 ? DefaultBatchGroupsCount : (int)Math.Ceiling(BatchRowCount / divisionConf.population);
        return groupsCount;
    }
}

public class RefreshUserDivisionMessage
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long PlayerId { get; set; }
    public short ShardId { get; set; }
    public int SeasonToBeRefreshed { get; set; }
    public int Rank { get; set; }
    public int UsersCount { get; set; }
}

public class RefreshWorldRankMessage
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long PlayerId { get; set; }
    public int SeasonToBeRefreshed { get; set; }
    public int WorldRank { get; set; } // 世界排名，从0开始
}