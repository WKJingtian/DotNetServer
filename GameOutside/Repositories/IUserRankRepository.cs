using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Util;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Repositories;

public interface IUserRankRepository
{
    /// <summary>
    /// 根据 ShardId 和 PID 获取指定赛季的玩家排名信息
    /// </summary>
    /// <param name="seasonNumber">赛季编号，为空时获取当前赛季的</param>
    /// <returns>用户排名实体，如果不存在则返回null</returns>
    ValueTask<UserRank?> GetUserRankAsync(short shardId, long playerId, int? seasonNumber = null);

    /// <summary>
    /// 移除指定赛季的玩家排名记录
    /// </summary>
    /// <returns>是否成功移除记录</returns>
    Task<int> RemoveUserRankAsync(short shardId, long playerId, int seasonNumber);

    /// <summary>
    /// 获取指定赛季、段位和多个分组的玩家排名列表
    /// </summary>
    /// <param name="division">段位号</param>
    /// <param name="groupIds">分组ID列表</param>
    /// <param name="seasonNumber">赛季编号</param>
    /// <returns>分组ID到用户排名列表的映射</returns
    Task<Dictionary<int, List<UserRank>>> BatchGetUserRankGroupsBySeasonAsync(short shardId, int division, List<long> groupIds, int seasonNumber, TrackingOptions trackingOptions);

    /// <summary>
    /// 获取用户在所在分段及分组的排名
    /// TODO: 缺少索引，先使用了 IX_UserRanks_Division_GroupId 索引，然后回表，进行了一次 filter，没有用到 INDEX "IX_UserRanks_HighestScore_Timestamp" ("HighestScore" DESC, "Timestamp" ASC)
    /// </summary>
    /// <returns>用户在分段内的排名，如果用户不存在返回-1</returns>
    Task<int> GetUserDivisionRankAsync(short shardId, long playerId, int? seasonNumber = null);

    /// <summary>
    /// 获取用户在所在分段及分组的排名列表
    /// </summary>
    /// <returns>用户排名列表</returns>
    Task<List<UserRank>> GetUserDivisionRankGroupAsync(short shardId, long playerId, int? seasonNumber = null);

    /// <summary>
    /// 添加用户排名记录
    /// </summary>
    /// <param name="userRank">用户排名实体</param>
    void AddUserRank(UserRank userRank);

    /// <summary>
    /// 保留参数指定的多个赛季号的数据，清除其他赛季号的所有用户排名数据
    /// </summary>
    ValueTask<bool> ClearUserRanksAsync(short shardId, IEnumerable<int> seasonNumbersToBeKept);
}

public class UserRankRepository(
    BuildingGameDB dbCtx,
    ISeasonInfoRepository seasonInfoRepository) : IUserRankRepository
{
    public ValueTask<UserRank?> GetUserRankAsync(short shardId, long playerId, int? seasonNumber = null)
    {
        // 如果没有指定赛季号，则获取当前赛季的排名
        seasonNumber ??= seasonInfoRepository.GetCurrentSeasonNumber();

        return dbCtx.UserRanks.FindAsync(playerId, shardId, seasonNumber);
    }

    public Task<int> RemoveUserRankAsync(short shardId, long playerId, int seasonNumber)
    {
        return dbCtx.UserRanks
            .Where(rank => rank.PlayerId == playerId && rank.ShardId == shardId && rank.SeasonNumber == seasonNumber)
            .ExecuteDeleteAsync();
    }

    /// <summary>
    /// 获取指定集群、赛季、段位和分组的玩家排名列表
    /// </summary>
    /// <param name="shardId"></param>
    /// <param name="division">段位号</param>
    /// <param name="groupId">分组ID</param>
    /// <param name="seasonNumber">赛季编号，为空时获取当前赛季的</param>
    /// <returns>用户排名列表</returns>
    private Task<List<UserRank>> GetUserRankGroupAsync(short shardId, int division, long groupId, int? seasonNumber)
    {
        seasonNumber ??= seasonInfoRepository.GetCurrentSeasonNumber();
        return dbCtx.UserRanks
                .Where(rank => rank.ShardId == shardId && rank.GroupId == groupId && rank.Division == division && seasonNumber == rank.SeasonNumber)
                .ToListAsync();
    }

    public async Task<int> GetUserDivisionRankAsync(short shardId, long playerId, int? seasonNumber)
    {
        seasonNumber ??= seasonInfoRepository.GetCurrentSeasonNumber();
        var userRank = await GetUserRankAsync(shardId, playerId, seasonNumber);
        if (userRank == null)
            return -1;
        int divisionRank = await dbCtx.UserRanks.Where(rank =>
                rank.ShardId == shardId &&
                rank.SeasonNumber == seasonNumber &&
                rank.Division == userRank.Division && rank.GroupId == userRank.GroupId &&
                (rank.HighestScore > userRank.HighestScore || rank.HighestScore == userRank.HighestScore &&
                    rank.Timestamp < userRank.Timestamp))
                .CountAsync();
        return divisionRank;
    }

    public async Task<List<UserRank>> GetUserDivisionRankGroupAsync(short shardId, long playerId, int? seasonNumber = null)
    {
        seasonNumber ??= seasonInfoRepository.GetCurrentSeasonNumber();
        var userRank = await GetUserRankAsync(shardId, playerId, seasonNumber);
        if (userRank == null)
            return [];
        return await GetUserRankGroupAsync(shardId, userRank.Division, userRank.GroupId, seasonNumber);
    }

    public void AddUserRank(UserRank userRank)
    {
        dbCtx.UserRanks.Add(userRank);
    }

    private const int BatchSize = 1000; // 分批清理，每次删除1000条记录
    public async ValueTask<bool> ClearUserRanksAsync(short shardId, IEnumerable<int> seasonNumbersToBeKept)
    {
        // 限制清理的最大赛季号，防止结算时已到下个赛季的极端情况
        int maxSeasonNumberToBeKept = seasonNumbersToBeKept.Max();
        await ClearUserRanksByShardIdAsync(shardId, maxSeasonNumberToBeKept, seasonNumbersToBeKept);

        return true;
    }

    private async ValueTask ClearUserRanksByShardIdAsync(short shardId, int maxSeasonNumberToBeKept, IEnumerable<int> seasonNumbersToBeKept)
    {
        int deletedCount = 0;
        do
        {
            deletedCount = await dbCtx.UserRanks
                .Where(rank => rank.ShardId == shardId && !seasonNumbersToBeKept.Contains(rank.SeasonNumber) && rank.SeasonNumber <= maxSeasonNumberToBeKept)
                .OrderBy(rank => rank.PlayerId)
                .Take(BatchSize)
                .ExecuteDeleteAsync();
            await Task.Delay(5);
        } while (deletedCount > 0);
    }

    private async ValueTask ClearAllShardsUserRanksAsync(int maxSeasonNumberToBeKept, IEnumerable<int> seasonNumbersToBeKept)
    {
        foreach (var shardId in Consts.ShardIds)
        {
            await ClearUserRanksByShardIdAsync(shardId, maxSeasonNumberToBeKept, seasonNumbersToBeKept);
        }
    }

    public async Task<Dictionary<int, List<UserRank>>> BatchGetUserRankGroupsBySeasonAsync(short shardId, int division, List<long> groupIds, int seasonNumber, TrackingOptions trackingOptions)
    {
        var query = trackingOptions == TrackingOptions.NoTracking
            ? dbCtx.UserRanks.AsNoTracking()
            : dbCtx.UserRanks;
        var batchRanks = await query
            .Where(rank => rank.ShardId == shardId && rank.Division == division && groupIds.Contains(rank.GroupId) && rank.SeasonNumber == seasonNumber)
            .ToListAsync();

        return batchRanks
            .GroupBy(rank => (int)rank.GroupId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
