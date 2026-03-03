using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Infra.ApiController;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Services;
using GameOutside.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameOutside.Controllers;

[Authorize]
public class DivisionController(
    IConfiguration configuration,
    BuildingGameDB context,
    ServerConfigService serverConfigService,
    LeaderboardModule leaderboardModule,
    UserRankService userRankService,
    DivisionService divisionService,
    SeasonService seasonService,
    UserEndlessRankService userEndlessRankService,
    ILogger<DivisionController> logger,
    UserInfoService userInfoService
) : BaseApiController(configuration)
{
    public record struct UserDivisionReply(int DivisionScore, int Division, int SubDivision);

    [HttpPost]
    public async Task<ActionResult<UserDivisionReply>> GetUserDivisionInfo()
    {
        var divisionInfo = await divisionService.GetUserDivisionAsync(PlayerShard, PlayerId, TrackingOptions.NoTracking);
        if (divisionInfo == null)
            return BadRequest(new ErrorResponse() { ErrorCode = (int)ErrorKind.NO_USER_RECORDS });
        return Ok(new UserDivisionReply()
        {
            DivisionScore = divisionInfo.DivisionScore,
            Division = serverConfigService.GetDivisionByDivisionScore(divisionInfo.DivisionScore),
            SubDivision = -1
        });
    }

    public record struct UserRankResponseUnit(UserInfo UserInfo, Int64 HighestScore, int Rank);

    public record struct GetUserDivisionGroupInfoResponse(List<UserRankResponseUnit> UserList, long GroupId, float Percentage);

    /// <summary>
    /// 获取玩家所在段位和分组的玩家信息列表
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<GetUserDivisionGroupInfoResponse>> GetUserDivisionGroup()
    {
        // 需要兼容青铜段位的特殊情况
        var division
            = await divisionService.GetDivisionNumberAsync(PlayerShard, PlayerId,
                CreateOptions.DoNotCreateWhenNotExists);
        var seasonNumber = seasonService.GetCurrentSeasonNumberByDivision(division);
        var divisionGroup
            = await userRankService.GetUserDivisionRankGroupBySeasonAsync(PlayerShard, PlayerId, seasonNumber);
        if (divisionGroup.Count <= 0)
            return Ok(new GetUserDivisionGroupInfoResponse() { UserList = [], GroupId = -1, Percentage = 0 });

        var userRankList = await GetUserRankListWithUserInfoAsync(divisionGroup);
        if (userRankList.Count <= 0)
            return Ok(new GetUserDivisionGroupInfoResponse() { UserList = [], GroupId = -1, Percentage = 0 });

        return Ok(new GetUserDivisionGroupInfoResponse { UserList = userRankList, GroupId = divisionGroup.First().GroupId });
    }

    private async Task<List<UserRankResponseUnit>> GetUserRankListWithUserInfoAsync(List<UserRank> userRanks)
    {
        // 按 ShardId 分组，批量获取用户信息
        var groupedByShardId = userRanks.GroupBy(ur => ur.ShardId);
        var userRankList = new List<UserRankResponseUnit>();
        int rank = 0;
        foreach (var shardGroup in groupedByShardId)
        {
            var shardId = shardGroup.Key;
            var userRanksInShard = shardGroup.ToList();

            try
            {
                var userInfoDict = await userInfoService.GetUserInfosByPlayerIdsAsync(shardId, userRanksInShard.Select(ur => ur.PlayerId));

                // 为该 shard 的用户创建 UserRankResponseUnit 列表
                foreach (var userRank in userRanksInShard)
                {
                    if (userInfoDict.TryGetValue(userRank.PlayerId, out var userInfo))
                    {
                        userRankList.Add(new UserRankResponseUnit
                        {
                            HighestScore = userRank.HighestScore,
                            UserInfo = userInfo,
                            Rank = rank++,
                        });
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error fetching user info for shard {ShardId}", shardId);
            }
        }

        return userRankList;
    }

    private (int, int) GetNearestRankRangeInclusiveExclusive(int rank, int count, int max)
    {
        if (max <= 0)
            return (-1, -1);
        if (max <= count)
            return (0, max);
        var start = rank - count / 2;
        var end = rank + count / 2;
        start = Math.Clamp(start, 0, max - count);
        end = Math.Clamp(end, count, max);
        return (start, end);
    }

    private List<UserRankResponseUnit> ConstructUserRankList(PlayerRank[] top50, Dictionary<long, UserInfo> userDic, int startRank)
    {
        var userRankList = new List<UserRankResponseUnit>();
        foreach (var playerRank in top50)
        {
            if (!userDic.TryGetValue(playerRank.PlayerId, out var userInfo))
                continue;
            userRankList.Add(new UserRankResponseUnit()
            {
                HighestScore = playerRank.Score,
                Rank = startRank++,
                UserInfo = userInfo
            });
        }
        return userRankList;
    }

    [HttpPost]
    public async Task<ActionResult<GeneralRankTopNResponse>> GetGlobalNormalRankNearSelf()
    {
        var selfScore = -1L;
        var division = await divisionService.GetDivisionNumberAsync(PlayerShard, PlayerId, CreateOptions.DoNotCreateWhenNotExists);
        var userRankInfo = await userRankService.GetCurrentSeasonUserRankByDivisionAsync(PlayerShard, PlayerId, division);
        if (userRankInfo != null)
            selfScore = userRankInfo.HighestScore;
        // 青铜段位的特殊赛季号
        var seasonNumber = seasonService.GetCurrentSeasonNumberByDivision(division);
        var selfRank = await leaderboardModule.GetPlayerRank(PlayerId, LeaderboardModule.NormalModeLeaderBoardId, seasonNumber);
        var playerCount = await leaderboardModule.GetLeaderBoardCardinality(LeaderboardModule.NormalModeLeaderBoardId, seasonNumber);

        var range = GetNearestRankRangeInclusiveExclusive(playerCount, 50, playerCount);
        if (selfRank > 0)
            range = GetNearestRankRangeInclusiveExclusive(selfRank, 50, playerCount);
        if (range.Item1 < 0 || range.Item2 < 0)
            return Ok(new GeneralRankTopNResponse { UserList = new List<UserRankResponseUnit>(), Percentage = -1, SelfScore = selfScore });
        var top50 = await leaderboardModule.GetTopPlayers(range.Item1, range.Item2, LeaderboardModule.NormalModeLeaderBoardId, seasonNumber);
        var playerIdList = top50.Select(rank => rank.PlayerId);
        var userDic = await userInfoService.BatchGetUserInfosByPlayerIdsAsync(playerIdList);
        var userRankList = ConstructUserRankList(top50, userDic, range.Item1);
        var percentage = await leaderboardModule.GetPlayerRankPercentage(selfScore, LeaderboardModule.NormalModeLeaderBoardId, seasonNumber);
        return Ok(new GeneralRankTopNResponse { UserList = userRankList, Percentage = percentage, SelfScore = selfScore });
    }

    public record struct GeneralRankTopNResponse(List<UserRankResponseUnit> UserList, long SelfScore, float Percentage);

    [HttpPost]
    public async Task<ActionResult<GeneralRankTopNResponse>> GetSurvivorRankNearSelf()
    {
        var userEndlessRank = await userEndlessRankService.GetCurrentSeasonUserEndlessRankAsync(PlayerShard, PlayerId);
        long selfScore = -1;
        if (userEndlessRank != null)
            selfScore = userEndlessRank.SurvivorScore;
        var selfRank = await leaderboardModule.GetPlayerRank(PlayerId, LeaderboardModule.SurvivorModeLeaderBoardId);
        var playerCount = await leaderboardModule.GetLeaderBoardCardinality(LeaderboardModule.SurvivorModeLeaderBoardId);
        var range = GetNearestRankRangeInclusiveExclusive(playerCount, 50, playerCount);
        if (selfRank > 0)
            range = GetNearestRankRangeInclusiveExclusive(selfRank, 50, playerCount);
        if (range.Item1 < 0 || range.Item2 < 0)
            return Ok(new GeneralRankTopNResponse { UserList = new List<UserRankResponseUnit>(), Percentage = -1, SelfScore = selfScore });
        var top50 = await leaderboardModule.GetTopPlayers(range.Item1, range.Item2, LeaderboardModule.SurvivorModeLeaderBoardId);
        var playerIdList = top50.Select(rank => rank.PlayerId);
        var userDic = await userInfoService.BatchGetUserInfosByPlayerIdsAsync(playerIdList);
        var userRankList = ConstructUserRankList(top50, userDic, range.Item1);
        var percentage = await leaderboardModule.GetPlayerRankPercentage(selfScore, LeaderboardModule.SurvivorModeLeaderBoardId);
        return Ok(new GeneralRankTopNResponse { UserList = userRankList, SelfScore = selfScore, Percentage = percentage });
    }

    [HttpPost]
    public async Task<ActionResult<GeneralRankTopNResponse>> GetTowerDefenceRankNearSelf()
    {
        var userEndlessRank = await userEndlessRankService.GetCurrentSeasonUserEndlessRankAsync(PlayerShard, PlayerId);
        long selfScore = -1;
        if (userEndlessRank != null)
            selfScore = userEndlessRank.TowerDefenceScore;
        var selfRank = await leaderboardModule.GetPlayerRank(PlayerId, LeaderboardModule.TowerDefenceModeLeaderBoardId);
        var playerCount = await leaderboardModule.GetLeaderBoardCardinality(LeaderboardModule.TowerDefenceModeLeaderBoardId);
        var range = GetNearestRankRangeInclusiveExclusive(playerCount, 50, playerCount);
        if (selfRank > 0)
            range = GetNearestRankRangeInclusiveExclusive(selfRank, 50, playerCount);
        if (range.Item1 < 0 || range.Item2 < 0)
            return Ok(new GeneralRankTopNResponse { UserList = new List<UserRankResponseUnit>(), Percentage = -1, SelfScore = selfScore });
        var top50 = await leaderboardModule.GetTopPlayers(range.Item1, range.Item2, LeaderboardModule.TowerDefenceModeLeaderBoardId);
        var playerIdList = top50.Select(rank => rank.PlayerId);
        var userDic = await userInfoService.BatchGetUserInfosByPlayerIdsAsync(playerIdList);
        var userRankList = ConstructUserRankList(top50, userDic, range.Item1);
        var percentage = await leaderboardModule.GetPlayerRankPercentage(selfScore, LeaderboardModule.TowerDefenceModeLeaderBoardId);
        return Ok(new GeneralRankTopNResponse { UserList = userRankList, Percentage = percentage, SelfScore = selfScore });
    }

    [HttpPost]
    public async Task<ActionResult<GeneralRankTopNResponse>> GetTrueEndlessRankNearSelf()
    {
        var userEndlessRank = await userEndlessRankService.GetCurrentSeasonUserEndlessRankAsync(PlayerShard, PlayerId);
        long selfScore = -1;
        if (userEndlessRank != null)
            selfScore = userEndlessRank.TrueEndlessScore;
        var selfRank = await leaderboardModule.GetPlayerRank(PlayerId, LeaderboardModule.TrueEndlessModeLeaderBoardId);
        var playerCount = await leaderboardModule.GetLeaderBoardCardinality(LeaderboardModule.TrueEndlessModeLeaderBoardId);
        var range = GetNearestRankRangeInclusiveExclusive(playerCount, 50, playerCount);
        if (selfRank > 0)
            range = GetNearestRankRangeInclusiveExclusive(selfRank, 50, playerCount);
        if (range.Item1 < 0 || range.Item2 < 0)
            return Ok(new GeneralRankTopNResponse { UserList = new List<UserRankResponseUnit>(), Percentage = -1, SelfScore = selfScore });
        var top50 = await leaderboardModule.GetTopPlayers(range.Item1, range.Item2, LeaderboardModule.TrueEndlessModeLeaderBoardId);
        var playerIdList = top50.Select(rank => rank.PlayerId);
        var userDic = await userInfoService.BatchGetUserInfosByPlayerIdsAsync(playerIdList);
        var userRankList = ConstructUserRankList(top50, userDic, range.Item1);
        var percentage = await leaderboardModule.GetPlayerRankPercentage(selfScore, LeaderboardModule.TrueEndlessModeLeaderBoardId);
        return Ok(new GeneralRankTopNResponse { UserList = userRankList, Percentage = percentage, SelfScore = selfScore });
    }

    public record struct CheckDivisionRewardResponse(
        bool HaveReward,
        bool NotifyDivisionChange,
        int LastDivisionRank,
        int LastEndlessRank,
        int LastWorldRank,
        int LastDivisionScore,
        GeneralReward? RankReward,
        GeneralReward? DivisionReward,
        GeneralReward? EndlessReward);
}