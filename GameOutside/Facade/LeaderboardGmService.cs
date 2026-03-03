using ChillyRoom.BuildingGame.v1.GM;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Services;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Facade;

public delegate Task<string?> LeaderboardCleaner(long playerId, short shardId, UserEndlessRank? userEndlessRank);

public class LeaderboardGmService(
    ILogger<LeaderboardGmService> logger,
    BuildingGameDB dbContext,
    PlayerModule playerModule,
    LeaderboardModule leaderboardModule,
    DivisionService divisionService,
    SeasonService seasonService,
    UserRankService userRankService,
    UserEndlessRankService userEndlessRankService) : Leaderboard.LeaderboardBase
{
    private Dictionary<string, LeaderboardCleaner> LeaderboardCleaners => new()
    {
        ["世界榜"] = CleanNormalLeaderboard,
        ["十面埋伏榜"] = CleanSurvivorLeaderboard,
        ["墨浪坚防榜"] = CleanTowerDefenceLeaderboard,
        ["无尽模式榜"] = CleanTrueEndlessLeaderboard,
    };

    public override Task<GetLeaderboardTypesResponse> GetLeaderboardTypes(GetLeaderboardTypesRequest request, ServerCallContext context)
    {
        var response = new GetLeaderboardTypesResponse();
        response.LeaderboardTypes.AddRange(LeaderboardCleaners.Keys);
        return Task.FromResult(response);
    }

    public override async Task<GetLeaderboardWithPaginationResponse> GetLeaderboardWithPagination(GetLeaderboardWithPaginationRequest request, ServerCallContext context)
    {
        if (!LeaderboardCleaners.ContainsKey(request.LeaderboardType))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"不支持的排行榜类型: {request.LeaderboardType}"));
        }

        if (request.RankStartIndex < 1)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "排名起始位置必须大于等于1"));
        }

        if (request.NumberOfRanks <= 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "返回排名数量必须大于0"));
        }

        var leaderboardId = GetLeaderboardId(request.LeaderboardType);
        var seasonNumber = seasonService.GetCurrentSeasonNumber();

        // 转换为从0开始的索引
        int startIndex = request.RankStartIndex - 1;
        int endIndex = startIndex + request.NumberOfRanks;

        var topPlayers = await leaderboardModule.GetTopPlayers(startIndex, endIndex, leaderboardId, seasonNumber);

        var response = new GetLeaderboardWithPaginationResponse();
        foreach (var player in topPlayers)
        {
            response.Ranks.Add(new Rank
            {
                PlayerId = player.PlayerId,
                Score = player.Score
            });
        }

        return response;
    }

    public override async Task<RemovePlayersFromLeaderboardResponse> RemovePlayersFromLeaderboard(RemovePlayersFromLeaderboardRequest request, ServerCallContext context)
    {
        var now = DateTime.UtcNow;
        var beijingTime = now.AddHours(8);
        // 避开北京时间18:30至19:30的时间段，防止与排行榜结算冲突
        if (beijingTime.Hour == 18 && beijingTime.Minute >= 30 ||
            beijingTime.Hour == 19 && beijingTime.Minute <= 30)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "当前接近排行榜结算时间段，无法进行清理操作，请避开北京时间18:30至19:30的时间段后重试"));
        }

        if (request.PlayerIds.Count == 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "从排行榜移除的 PID 列表不能为空"));
        }

        foreach (var leaderboardType in request.LeaderboardTypes)
        {
            if (!LeaderboardCleaners.ContainsKey(leaderboardType))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"不支持的排行榜类型: {leaderboardType}"));
            }
        }

        foreach (var playerId in request.PlayerIds)
        {
            if (playerId <= 0)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"无效的玩家ID: {playerId}"));
            }
        }

        var response = new RemovePlayersFromLeaderboardResponse();

        dbContext.Database.SetCommandTimeout(1200); // 设置数据库超时时间为20分钟，防止大批量删除时超时
        return await dbContext.WithRCUDefaultRetry(async _ =>
        {
            // 使用 ExecuteDeleteAsync 批量删除用户排名数据，需要提前开启事务
            await using var t = await dbContext.Database.BeginTransactionAsync();

            foreach (var playerId in request.PlayerIds)
            {
                var shardId = await playerModule.GetPlayerShardId(playerId);
                if (!shardId.HasValue)
                {
                    response.FailedList.Add(new RemoveFailedInfo
                    {
                        PlayerId = playerId,
                        Reason = "玩家不存在"
                    });
                    continue;
                }

                UserEndlessRank? endlessRank = null;
                if (request.LeaderboardTypes.Contains("十面埋伏榜") ||
                    request.LeaderboardTypes.Contains("墨浪坚防榜") ||
                    request.LeaderboardTypes.Contains("无尽模式榜"))
                {
                    endlessRank = await userEndlessRankService.GetCurrentSeasonUserEndlessRankAsync(shardId.Value, playerId);
                }

                foreach (var leaderboardType in request.LeaderboardTypes)
                {
                    var cleaner = LeaderboardCleaners[leaderboardType];
                    var failedReason = await cleaner(playerId, shardId.Value, endlessRank);
                    if (failedReason != null)
                    {
                        response.FailedList.Add(new RemoveFailedInfo
                        {
                            LeaderboardType = leaderboardType,
                            PlayerId = playerId,
                            Reason = failedReason
                        });
                    }
                }
            }

            await dbContext.SaveChangesWithDefaultRetryAsync(false);
            await t.CommitAsync();
            dbContext.ChangeTracker.AcceptAllChanges();

            return response;
        });
    }

    /// <summary>
    /// 清理世界榜（包括段位分组榜）
    /// </summary>
    /// <returns>错误原因，如果没有错误，返回 null</returns>
    private async Task<string?> CleanNormalLeaderboard(long playerId, short shardId, UserEndlessRank? _)
    {
        try
        {
            var divisionNumber = await dbContext.WithDefaultRetry(_ => divisionService.GetDivisionNumberAsync(shardId, playerId, CreateOptions.DoNotCreateWhenNotExists));
            var seasonNumber = seasonService.GetCurrentSeasonNumberByDivision(divisionNumber);

            await userRankService.RemoveUserRankAsync(shardId, playerId, seasonNumber);
            await leaderboardModule.RemovePlayerFromLeaderBoard(playerId, LeaderboardModule.NormalModeLeaderBoardId, seasonNumber);

            return null;
        }
        catch (Exception e)
        {
            logger.LogError(e, "清理世界榜失败，PlayerId: {PlayerId}", playerId);
            return e.Message;
        }
    }

    /// <summary>
    /// 清理十面埋伏榜
    /// </summary>
    /// <returns>错误原因，如果没有错误，返回 null</returns>
    private async Task<string?> CleanSurvivorLeaderboard(long playerId, short shardId, UserEndlessRank? endlessRank)
    {
        try
        {
            if (endlessRank != null)
            {
                endlessRank.SurvivorScore = 0;
                int currentSeasonNumber = seasonService.GetCurrentSeasonNumber();
                await leaderboardModule.RemovePlayerFromLeaderBoard(playerId, LeaderboardModule.SurvivorModeLeaderBoardId, currentSeasonNumber);
            }
            return null;
        }
        catch (Exception e)
        {
            logger.LogError(e, "清理十面埋伏榜失败，PlayerId: {PlayerId}", playerId);
            return e.Message;
        }
    }

    /// <summary>
    /// 清理墨浪坚防榜
    /// </summary>
    /// <returns>错误原因，如果没有错误，返回 null</returns>
    private async Task<string?> CleanTowerDefenceLeaderboard(long playerId, short shardId, UserEndlessRank? endlessRank)
    {
        try
        {
            if (endlessRank != null)
            {
                endlessRank.TowerDefenceScore = 0;
                int currentSeasonNumber = seasonService.GetCurrentSeasonNumber();
                await leaderboardModule.RemovePlayerFromLeaderBoard(playerId, LeaderboardModule.TowerDefenceModeLeaderBoardId, currentSeasonNumber);
            }
            return null;
        }
        catch (Exception e)
        {
            logger.LogError(e, "清理墨浪坚防榜失败，PlayerId: {PlayerId}", playerId);
            return e.Message;
        }
    }

    /// <summary>
    /// 清理无尽模式榜
    /// </summary>
    /// <returns>错误原因，如果没有错误，返回 null</returns>
    private async Task<string?> CleanTrueEndlessLeaderboard(long playerId, short shardId, UserEndlessRank? endlessRank)
    {
        try
        {
            if (endlessRank != null)
            {
                endlessRank.TrueEndlessScore = 0;
                int currentSeasonNumber = seasonService.GetCurrentSeasonNumber();
                await leaderboardModule.RemovePlayerFromLeaderBoard(playerId, LeaderboardModule.TrueEndlessModeLeaderBoardId, currentSeasonNumber);
            }
            return null;
        }
        catch (Exception e)
        {
            logger.LogError(e, "清理无尽模式榜失败，PlayerId: {PlayerId}", playerId);
            return e.Message;
        }
    }

    /// <summary>
    /// 根据排行榜类型名称获取对应的 LeaderboardModule ID
    /// </summary>
    private static string GetLeaderboardId(string leaderboardType)
    {
        return leaderboardType switch
        {
            "世界榜" => LeaderboardModule.NormalModeLeaderBoardId,
            "十面埋伏榜" => LeaderboardModule.SurvivorModeLeaderBoardId,
            "墨浪坚防榜" => LeaderboardModule.TowerDefenceModeLeaderBoardId,
            "无尽模式榜" => LeaderboardModule.TrueEndlessModeLeaderBoardId,
            _ => throw new ArgumentException($"不支持的排行榜类型: {leaderboardType}")
        };
    }
}