using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.DBContext;
using GameOutside.Models;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Repositories;

public interface IUserCardRepository
{
    public Task<List<UserCard>> GetUserCardsAsync(short shardId, long playerId, TrackingOptions trackingOptions);
    public Task<List<UserCard>> GetUserCardsByCardIdsAsync(
        short shardId,
        long playerId,
        IEnumerable<int> cardIds,
        TrackingOptions trackingOptions);
    public Task UpsertUserCardsAsync(IEnumerable<UserCard> userCards);
}

public class UserCardRepository(BuildingGameDB dbCtx) : IUserCardRepository
{
    /// <summary>
    /// 应用查询配置选项
    /// </summary>
    private static IQueryable<UserCard> ApplyQueryOptions<T>(
        IQueryable<T> query,
        TrackingOptions trackingOptions) where T : UserCard
    {
        var typedQuery = query.Cast<UserCard>();

        if (trackingOptions == TrackingOptions.NoTracking)
        {
            typedQuery = typedQuery.AsNoTracking();
        }

        return typedQuery;
    }

    public Task<List<UserCard>> GetUserCardsAsync(short shardId, long playerId, TrackingOptions trackingOptions)
    {
        var query = dbCtx.UserCards.Where(uc => uc.ShardId == shardId && uc.PlayerId == playerId);
        query = ApplyQueryOptions(query, trackingOptions);
        return query.ToListAsync();
    }

    public Task<List<UserCard>> GetUserCardsByCardIdsAsync(
        short shardId,
        long playerId,
        IEnumerable<int> cardIds,
        TrackingOptions trackingOptions)
    {
        var query = dbCtx.UserCards.Where(uc =>
            uc.ShardId == shardId &&
            uc.PlayerId == playerId &&
            cardIds.Contains(uc.CardId));
        query = ApplyQueryOptions(query, trackingOptions);
        return query.ToListAsync();
    }

    public Task UpsertUserCardsAsync(IEnumerable<UserCard> userCards)
    {
        return dbCtx.UserCards.UpsertRange(userCards).RunAsync();
    }
}