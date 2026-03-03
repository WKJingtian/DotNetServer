using System.Runtime.CompilerServices;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.DBContext;
using GameOutside.Models;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Repositories;

public interface IUserDivisionRepository
{
    /// <summary>
    /// 使用 ShardId 和 PID 获取玩家的段位信息
    /// </summary>
    /// <returns>用户段位实体，如果不存在则返回null</returns>
    Task<UserDivision?> GetUserDivisionAsync(short shardId, long playerId, TrackingOptions trackingOptions);

    /// <summary>
    /// 使用 ShardId 和 PID 获取玩家的段位分数 
    /// </summary>
    /// <returns>用户段位分数，如果不存在则返回null</returns>
    Task<int?> GetUserDivisonScoreAsync(short shardId, long playerId, CreateOptions createOptions);

    /// <summary>
    /// 创建新的用户段位记录
    /// </summary>
    /// <param name="divisionScore">初始段位分数</param>
    /// <returns>新创建的用户段位实体</returns>
    UserDivision CreateUserDivision(short shardId, long playerId, int divisionScore);
}

public class UserDivisionRepository(BuildingGameDB dbCtx) : IUserDivisionRepository
{
    private static readonly Func<BuildingGameDB, short, long, Task<UserDivision?>> GetUserDivision =
        EF.CompileAsyncQuery((BuildingGameDB db, short shardId, long playerId) =>
            db.UserDivisions.FirstOrDefault(ud => ud.ShardId == shardId && ud.PlayerId == playerId));
    private static readonly Func<BuildingGameDB, short, long, Task<UserDivision?>> GetUserDivisionNoTracking =
        EF.CompileAsyncQuery((BuildingGameDB db, short shardId, long playerId) =>
            db.UserDivisions.AsNoTracking().FirstOrDefault(ud => ud.ShardId == shardId && ud.PlayerId == playerId));

    private static readonly Func<BuildingGameDB, short, long, Task<int?>> GetUserDivisonScore =
        EF.CompileAsyncQuery((BuildingGameDB db, short shardId, long playerId) =>
            db.UserDivisions
                .AsNoTracking()
                .Where(ud => ud.ShardId == shardId && ud.PlayerId == playerId)
                .Select(ud => (int?)ud.DivisionScore)
                .FirstOrDefault());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<UserDivision?> GetUserDivisionAsync(short shardId, long playerId, TrackingOptions trackingOptions)
    {
        return trackingOptions == TrackingOptions.NoTracking ? GetUserDivisionNoTracking(dbCtx, shardId, playerId) : GetUserDivision(dbCtx, shardId, playerId);
    }

    public async Task<int?> GetUserDivisonScoreAsync(short shardId, long playerId, CreateOptions createOptions)
    {
        var divisionScore = await GetUserDivisonScore(dbCtx, shardId, playerId);
        if (!divisionScore.HasValue && createOptions == CreateOptions.CreateWhenNotExists)
        {
            var userDivision = CreateUserDivision(shardId, playerId, 0);
            return userDivision.DivisionScore;
        }
        return divisionScore;
    }

    public UserDivision CreateUserDivision(short shardId, long playerId, int divisionScore)
    {
        var userDivision = new UserDivision
        {
            ShardId = shardId,
            PlayerId = playerId,
            DivisionScore = divisionScore,
            MaxDivisionScore = divisionScore,
            LastSeasonNumber = 0,
            RewardReceived = true,
            LastDivisionScore = 0,
            LastDivisionRank = 0,
            LastWorldRank = 0
        };

        dbCtx.UserDivisions.Add(userDivision);
        return userDivision;
    }
}
