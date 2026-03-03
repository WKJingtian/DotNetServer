using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.DBContext;
using GameOutside.Models;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Repositories;

public interface IUserItemRepository
{
    public Task<List<UserItem>> GetUserItemsAsync(short shardId, long playerId, TrackingOptions trackingOptions);

    public Task<bool> HasItemAsync(short shardId, long playerId, int itemId);
}

public class UserItemRepository(BuildingGameDB dbCtx) : IUserItemRepository
{
    /// <summary>
    /// 应用查询配置选项
    /// </summary>
    private static IQueryable<UserItem> ApplyQueryOptions<T>(
        IQueryable<T> query,
        TrackingOptions trackingOptions) where T : UserItem
    {
        var typedQuery = query.Cast<UserItem>();

        if (trackingOptions == TrackingOptions.NoTracking)
        {
            typedQuery = typedQuery.AsNoTracking();
        }

        return typedQuery;
    }

    public Task<List<UserItem>> GetUserItemsAsync(short shardId, long playerId, TrackingOptions trackingOptions)
    {
        var query = dbCtx.UserItems.Where(uc => uc.ShardId == shardId && uc.PlayerId == playerId);
        query = ApplyQueryOptions(query, trackingOptions);
        return query.ToListAsync();
    }

    public Task<bool> HasItemAsync(short shardId, long playerId, int itemId)
    {
        return dbCtx.UserItems.AnyAsync(uc => uc.ShardId == shardId && uc.PlayerId == playerId && uc.ItemId == itemId);
    }
}