using GameOutside.DBContext;
using GameOutside.Models;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Repositories;

public interface IBattlePassRepository
{
    public Task<List<UserBattlePassInfo>> GetAllUserBattlePassInfoAsync(short shardId, long playerId);
    public Task<UserBattlePassInfo?> GetUserBattlePassInfoByPassIdAsync(short? shardId, long playerId, int passId);
    public void AddUserBattlePassInfo(UserBattlePassInfo info);

    public UserBattlePassInfo CreateNewUserBattlePassInfo(
        short shardId, long playerId, int battlePassId, int superPassLevelCount);
}

public class BattlePassRepository(BuildingGameDB dbCtx) : IBattlePassRepository
{
    public Task<List<UserBattlePassInfo>> GetAllUserBattlePassInfoAsync(short shardId, long playerId)
    {
        return dbCtx.UserBattlePassInfos
            .Where(info => info.PlayerId == playerId && info.ShardId == shardId)
            .ToListAsync();
    }

    public Task<UserBattlePassInfo?> GetUserBattlePassInfoByPassIdAsync(short? shardId, long playerId, int passId)
    {
        return dbCtx.UserBattlePassInfos.FirstOrDefaultAsync(u =>
            shardId.HasValue
                ? u.PlayerId == playerId && u.PassId == passId && u.ShardId == shardId
                : u.PlayerId == playerId && u.PassId == passId);
    }

    public void AddUserBattlePassInfo(UserBattlePassInfo info)
    {
        dbCtx.UserBattlePassInfos.AddAsync(info);
    }

    public UserBattlePassInfo CreateNewUserBattlePassInfo(
        short shardId,
        long playerId,
        int battlePassId,
        int superPassLevelCount)
    {
        var battlePassInfo = new UserBattlePassInfo()
        {
            ShardId = shardId,
            PlayerId = playerId,
            PassId = battlePassId,
            Exp = 0,
            ClaimStatus = new List<long>()
        };
        for (var i = 0; i < superPassLevelCount; ++i)
            battlePassInfo.ClaimStatus.Add(0);
        return battlePassInfo;
    }
}