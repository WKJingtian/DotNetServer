using GameExternal;
using GameOutside.DBContext;
using GameOutside.Models;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Repositories;

public interface IIapPackageRepository
{
    public void AddPromotionData(long playerId, short shardId);
    public Task<UserPaymentAndPromotionStatus?> GetPromotionData(long playerId, short? shardId);

    public Task<int> GetIapPurchaseCountWithinTimeAsync(
        short shardId, long playerId, string propId, IapCommodityConfig iapConfig, long timeZoneOffset);

    public Task<List<UserIapPurchaseRecord>> GetFullPurchaseListAsync(short shardId, long playerId);
    public void AddIapPurchaseRecord(short shardId, long playerId, string propId);
}

public class IapPackageRepository(BuildingGameDB dbCtx) : IIapPackageRepository
{

    public void AddPromotionData(long playerId, short shardId)
    {
        var status = new UserPaymentAndPromotionStatus
        {
            PlayerId = playerId,
            ShardId = shardId,
            LastPromotedPackage = "",
            PackagePromotionTime = 0,
            IceBreakingPayPromotion = 0,
        };
        dbCtx.PromotionStatus.Add(status);
    }

    public Task<UserPaymentAndPromotionStatus?> GetPromotionData(long playerId, short? shardId)
    {
        return dbCtx.PromotionStatus.FirstOrDefaultAsync(u =>
            shardId.HasValue ? u.ShardId == shardId && u.PlayerId == playerId : u.PlayerId == playerId);
    }


    public async Task<int> GetIapPurchaseCountWithinTimeAsync(
        short shardId,
        long playerId,
        string propId,
        IapCommodityConfig iapConfig,
        long timeZoneOffset)
    {
        var curTime = TimeUtils.GetCurrentTime();
        var list = await dbCtx.UserIapPurchases.AsNoTracking()
            .Where(x => x.PlayerId == playerId && x.ShardId == shardId && x.IapItemId == propId)
            .Select(x => x.WhenPurchased)
            .ToListAsync();
        return list.Count(purchaseTime => TimeUtils.IfRecordTimeIsInRange(iapConfig.limit_refresh_interval,
            iapConfig.sp_limit_refresh_rule, curTime, purchaseTime, timeZoneOffset));
    }

    public Task<List<UserIapPurchaseRecord>> GetFullPurchaseListAsync(short shardId, long playerId)
    {
        return dbCtx.UserIapPurchases
            .Where(x => x.PlayerId == playerId && x.ShardId == shardId)
            .ToListAsync();
    }

    public void AddIapPurchaseRecord(short shardId, long playerId, string propId)
    {
        var curTime = TimeUtils.GetCurrentTime();
        dbCtx.UserIapPurchases.Add(new UserIapPurchaseRecord
        {
            PlayerId = playerId, ShardId = shardId, IapItemId = propId, WhenPurchased = curTime,
        });
    }
}