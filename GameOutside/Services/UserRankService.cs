using ChillyRoom.Functions.DBModel;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Repositories;
using GameOutside.Util;

namespace GameOutside.Services;

public class UserRankService(
    BuildingGameDB dbCtx,
    IUserRankRepository userRankRepository,
    IUserRankGroupRepository userRankGroupRepository,
    SeasonService seasonService)
{
    /// <summary>
    /// 获取当前段位的Rank信息
    /// </summary>
    /// <returns></returns>
    public ValueTask<UserRank?> GetCurrentSeasonUserRankByDivisionAsync(short shardId, long playerId, int division)
    {
        var seasonNumber = seasonService.GetCurrentSeasonNumberByDivision(division);
        return dbCtx.WithDefaultRetry(_ => userRankRepository.GetUserRankAsync(shardId, playerId, seasonNumber));
    }

    /// <summary>
    /// 创建当前赛季的玩家排名
    /// </summary>
    public async Task<UserRank> CreateUserRankAsync(
        short shardId,
        long playerId,
        int currentDivision,
        long highestScore,
        long timestamp,
        bool win,
        int? seasonNumber = null)
    {
        seasonNumber ??= seasonService.GetCurrentSeasonNumber();
        var userRank = await dbCtx.WithDefaultRetry(_ => userRankRepository.GetUserRankAsync(shardId, playerId, seasonNumber));
        if (userRank != null)
            return userRank;

        userRank = new UserRank()
        {
            ShardId = shardId,
            PlayerId = playerId,
            SeasonNumber = seasonNumber.Value,
            Division = currentDivision,
            GroupId = await userRankGroupRepository.GetGroupIdAsync(seasonNumber.Value, currentDivision, shardId),
            HighestScore = highestScore,
            Timestamp = timestamp,
            Win = win
        };
        userRankRepository.AddUserRank(userRank);
        return userRank;
    }

    /// <summary>
    /// 获取用户在所在分段及分组中的排名
    /// 如果是青铜，返回0
    /// </summary>
    public ValueTask<int> GetUserDivisionRankAsync(short shardId, long playerId, int? seasonNumber)
    {
        if (seasonNumber == SeasonService.BronzeSeasonNumber)
        {
            return ValueTask.FromResult(0);
        }
        return dbCtx.WithDefaultRetry(_ =>
            userRankRepository.GetUserDivisionRankAsync(shardId, playerId, seasonNumber));
    }

    /// <summary>
    /// 获取用户在所在分段及分组中的排名列表
    /// 如果是青铜，返回自身 UserRank
    /// </summary>
    public async ValueTask<List<UserRank>> GetUserDivisionRankGroupBySeasonAsync(
        short shardId,
        long playerId,
        int seasonNumber)
    {
        if (seasonNumber == SeasonService.BronzeSeasonNumber)
        {
            var userRank = await dbCtx.WithDefaultRetry(_ => userRankRepository.GetUserRankAsync(shardId, playerId, seasonNumber));
            return userRank != null ? [userRank] : [];
        }
        return await dbCtx.WithDefaultRetry(_ =>
            userRankRepository.GetUserDivisionRankGroupAsync(shardId, playerId, seasonNumber));
    }

    public ValueTask<bool> ClearUserRanksAsync(short shardId, IEnumerable<int> seasonNumbersToBeKept)
    {
        return dbCtx.WithDefaultRetry(_ => userRankRepository.ClearUserRanksAsync(shardId, seasonNumbersToBeKept));
    }

    public ValueTask<int> RemoveUserRankAsync(short shardId, long playerId, int seasonNumber)
    {
        return dbCtx.WithDefaultRetry(_ => userRankRepository.RemoveUserRankAsync(shardId, playerId, seasonNumber));
    }
}
