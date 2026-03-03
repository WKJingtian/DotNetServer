using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Util;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Repositories;

public interface IUserEndlessRankRepository
{
    /// <summary>
    /// 获取玩家无尽模式排名信息
    /// </summary>
    /// <param name="seasonNumber">赛季编号，为空时获取当前赛季的</param>
    /// <returns>玩家无尽模式排名实体，如果不存在则返回null</returns>
    Task<UserEndlessRank?> GetUserEndlessRankAsync(short shardId, long playerId, int? seasonNumber = null);

    /// <summary>
    /// 添加玩家无尽模式排名记录
    /// </summary>
    UserEndlessRank CreateEndlessRank(short shardId, long playerId, int? seasonNumber = null);

    /// <summary>
    /// 保留参数指定的多个赛季号的数据，清除其他赛季号的所有玩家无尽模式排名数据
    /// </summary>
    ValueTask<bool> ClearUserEndlessRanksAsync(short shardId, IEnumerable<int> seasonNumbersToBeKept);
}

public class UserEndlessRankRepository(
    BuildingGameDB dbCtx,
    ISeasonInfoRepository seasonInfoRepository) : IUserEndlessRankRepository
{
    public Task<UserEndlessRank?> GetUserEndlessRankAsync(short shardId, long playerId, int? seasonNumber = null)
    {
        seasonNumber ??= seasonInfoRepository.GetCurrentSeasonNumber();
        return dbCtx.UserEndlessRanks.FirstOrDefaultAsync(x => x.ShardId == shardId && x.PlayerId == playerId && x.SeasonNumber == seasonNumber);
    }

    public UserEndlessRank CreateEndlessRank(short shardId, long playerId, int? seasonNumber = null)
    {
        seasonNumber ??= seasonInfoRepository.GetCurrentSeasonNumber();
        var endlessRank = new UserEndlessRank()
        {
            ShardId = shardId,
            PlayerId = playerId,
            SeasonNumber = seasonNumber.Value,
        };
        dbCtx.UserEndlessRanks.Add(endlessRank);
        return endlessRank;
    }

    private const int BatchSize = 1000; // 分批清理，每次删除1000条记录
    public ValueTask<bool> ClearUserEndlessRanksAsync(short shardId, IEnumerable<int> seasonNumbersToBeKept)
    {
        // 限制清理的最大赛季号，防止结算时已到下个赛季的极端情况
        int maxSeasonNumberToBeKept = seasonNumbersToBeKept.Max();
        return ClearUserEndlessRanksByShardIdAsync(shardId, maxSeasonNumberToBeKept, seasonNumbersToBeKept);
    }

    private async ValueTask<bool> ClearUserEndlessRanksByShardIdAsync(short shardId, int maxSeasonNumberToBeKept, IEnumerable<int> seasonNumbersToBeKept)
    {
        int deletedCount = 0;
        do
        {
            deletedCount = await dbCtx.UserEndlessRanks
                .Where(rank => rank.ShardId == shardId && !seasonNumbersToBeKept.Contains(rank.SeasonNumber) && rank.SeasonNumber <= maxSeasonNumberToBeKept)
                .OrderBy(rank => rank.PlayerId)
                .Take(BatchSize)
                .ExecuteDeleteAsync();
            await Task.Delay(5);
        } while (deletedCount > 0);
        return true;
    }

    private async ValueTask<bool> ClearAllShardsUserEndlessRanksAsync(int maxSeasonNumberToBeKept, IEnumerable<int> seasonNumbersToBeKept)
    {
        foreach (var shardId in Consts.ShardIds)
        {
            await ClearUserEndlessRanksByShardIdAsync(shardId, maxSeasonNumberToBeKept, seasonNumbersToBeKept);
        }
        return true;
    }
}
