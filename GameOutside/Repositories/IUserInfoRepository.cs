using System.Runtime.CompilerServices;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.DBContext;
using GameOutside.Models;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Repositories;

/// <summary>
/// 用户信息查询联表选项
/// </summary>
public enum UserInfoIncludeOptions
{
    /// <summary>
    /// 不需要联表查询
    /// </summary>
    NoInclude,
    /// <summary>
    /// 需要联表查询 Histories
    /// </summary>
    IncludeHistories,
}

public interface IUserInfoRepository
{
    Task<bool> IsUserInfoExistsAsync(short? shardId, long playerId);
    Task<short?> GetShardIdAsync(long playerId);
    UserInfo CreateNewUserInfo(UserInfo userInfo);
    Task<UserInfo?> GetUserInfoAsync(short? shardId, long playerId, TrackingOptions trackingOptions, StaleReadOptions staleReadOptions, UserInfoIncludeOptions includeOptions);
    Task<int?> GetAvatarFrameItemIDAsync(short? shardId, long playerId);
    Task<int?> GetNameCardItemIDAsync(short? shardId, long playerId);
    // TODO: 和 GetUserInfosByPlayerIdsAsync 的性能对比，只保留一个
    Task<Dictionary<long, UserInfo>> GetUserInfosByPlayerIdsAsync(short shardId, IEnumerable<long> playerIds, TrackingOptions trackingOptions, StaleReadOptions staleReadOptions, UserInfoIncludeOptions includeOptions);
    Task<Dictionary<long, UserInfo>> BatchGetUserInfosByPlayerIdsAsync(IEnumerable<long> playerIds, TrackingOptions trackingOptions, StaleReadOptions staleReadOptions, UserInfoIncludeOptions includeOptions);
}

public class UserInfoRepository(BuildingGameDB dbCtx) : IUserInfoRepository
{
    /// <summary>
    /// 应用查询配置选项
    /// </summary>
    private static IQueryable<UserInfo> ApplyQueryOptions<T>(
        IQueryable<T> query,
        TrackingOptions trackingOptions,
        StaleReadOptions staleReadOptions,
        UserInfoIncludeOptions includeOptions) where T : UserInfo
    {
        var typedQuery = query.Cast<UserInfo>();

        if (includeOptions == UserInfoIncludeOptions.IncludeHistories)
        {
            typedQuery = typedQuery.Include(x => x.Histories);
        }
        else
        {
            typedQuery = typedQuery.IgnoreAutoIncludes();
        }

        if (trackingOptions == TrackingOptions.NoTracking)
        {
            typedQuery = typedQuery.AsNoTracking();
        }

        if (staleReadOptions == StaleReadOptions.AllowStaleRead)
        {
            typedQuery = typedQuery.AllowStaleRead();
        }
        else if (staleReadOptions == StaleReadOptions.Allow15sStaleRead)
        {
            typedQuery = typedQuery.AsOfSystemTime("-15s");
        }

        return typedQuery;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<bool> IsUserInfoExistsAsync(short? shardId, long playerId)
    {
        return dbCtx.UserInfos.AnyAsync(x => shardId.HasValue ? x.ShardId == shardId && x.PlayerId == playerId : x.PlayerId == playerId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<short?> GetShardIdAsync(long playerId)
    {
        return dbCtx.UserInfos
            .AsNoTracking()
            .IgnoreAutoIncludes()
            .Where(x => x.PlayerId == playerId)
            .Select(x => (short?)x.ShardId)
            .FirstOrDefaultAsync();
    }

    public UserInfo CreateNewUserInfo(UserInfo userInfo)
    {
        dbCtx.UserInfos.Add(userInfo);
        return userInfo;
    }

    public Task<UserInfo?> GetUserInfoAsync(short? shardId, long playerId, TrackingOptions trackingOptions, StaleReadOptions staleReadOptions, UserInfoIncludeOptions includeOptions)
    {
        var query = shardId.HasValue ?
            dbCtx.UserInfos.Where(x => x.ShardId == shardId.Value && x.PlayerId == playerId) :
            dbCtx.UserInfos.Where(x => x.PlayerId == playerId);

        query = ApplyQueryOptions(query, trackingOptions, staleReadOptions, includeOptions);

        return query.FirstOrDefaultAsync();
    }

    public Task<int?> GetAvatarFrameItemIDAsync(short? shardId, long playerId)
    {
        var query = shardId.HasValue
            ? dbCtx.UserInfos.Where(x => x.ShardId == shardId.Value && x.PlayerId == playerId)
            : dbCtx.UserInfos.Where(x => x.PlayerId == playerId);
        return query
            .AsNoTracking()
            .IgnoreAutoIncludes()
            .Select(x => (int?)x.AvatarFrameItemID)
            .FirstOrDefaultAsync();
    }

    public Task<int?> GetNameCardItemIDAsync(short? shardId, long playerId)
    {
        var query = shardId.HasValue
            ? dbCtx.UserInfos.Where(x => x.ShardId == shardId.Value && x.PlayerId == playerId)
            : dbCtx.UserInfos.Where(x => x.PlayerId == playerId);
        return query
            .AsNoTracking()
            .IgnoreAutoIncludes()
            .Select(x => (int?)x.NameCardItemID)
            .FirstOrDefaultAsync();
    }

    public Task<Dictionary<long, UserInfo>> BatchGetUserInfosByPlayerIdsAsync(IEnumerable<long> playerIds, TrackingOptions trackingOptions, StaleReadOptions staleReadOptions, UserInfoIncludeOptions includeOptions)
    {
        var query = dbCtx.UserInfos.Where(user => playerIds.Contains(user.PlayerId));
        query = ApplyQueryOptions(query, trackingOptions, staleReadOptions, includeOptions);

        return query.ToDictionaryAsync(user => user.PlayerId);
    }

    /// <summary>
    /// 根据 PID 列表获取用户信息
    /// </summary>
    /// <returns>用户信息字典，key 为 PID，value 为用户信息</returns>
    public Task<Dictionary<long, UserInfo>> GetUserInfosByPlayerIdsAsync(short shardId, IEnumerable<long> playerIds, TrackingOptions trackingOptions, StaleReadOptions staleReadOptions, UserInfoIncludeOptions includeOptions)
    {
        var query = dbCtx.UserInfos.Where(x => x.ShardId == shardId && playerIds.Contains(x.PlayerId));
        query = ApplyQueryOptions(query, trackingOptions, staleReadOptions, includeOptions);

        return query.ToDictionaryAsync(ui => ui.PlayerId, ui => ui);
    }
}