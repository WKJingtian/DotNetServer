using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.DBContext;
using Microsoft.EntityFrameworkCore;

public interface IPlatformItemRepository
{
    ValueTask<List<PlatformNotify>> GetNotifiesAsync(
        short? shardId,
        long playerId,
        ClaimStatus claimStatus,
        bool allowStaleRead = false,
        List<NotifyType>? types = null);
}

public class PlatformItemRepository(BuildingGameDB dbContext) : IPlatformItemRepository
{
    private static readonly ArgumentException NotifyTypeUnknown = new ArgumentException("Notify 类型为 Unknown");
    private static readonly ArgumentException ClaimStatusUnknown = new ArgumentException("查询的领取状态为 Unknown");

    public ValueTask<List<PlatformNotify>> GetNotifiesAsync(
        short? shardId,
        long playerId,
        ClaimStatus claimStatus,
        bool allowStaleRead = false,
        List<NotifyType>? types = null)
    {
        if (types != null && types.Any(t => t == NotifyType.Unknown))
            throw NotifyTypeUnknown;
        if (claimStatus == ClaimStatus.Unknown)
            throw ClaimStatusUnknown;
        IQueryable<PlatformNotify> query = allowStaleRead
            ? dbContext.Set<PlatformNotify>().AsOfSystemTime("-1s")
            : dbContext.Set<PlatformNotify>();
        IQueryable<PlatformNotify> queryable;
        if (types != null)
            queryable = query.Where(p =>
                shardId.HasValue
                    ? p.ShardId == shardId && p.PlayerId == playerId && (int)p.ClaimStatus == (int)claimStatus &&
                      types.Contains(p.Type)
                    : p.PlayerId == playerId && (int)p.ClaimStatus == (int)claimStatus && types.Contains(p.Type));
        else
            queryable = query.Where(p =>
                shardId.HasValue
                    ? p.ShardId == shardId && p.PlayerId == playerId && (int)p.ClaimStatus == (int)claimStatus
                    : p.PlayerId == playerId && (int)p.ClaimStatus == (int)claimStatus);
        query = queryable;
        return dbContext.WithDefaultRetry(_ => query.ToListAsync());
    }
}