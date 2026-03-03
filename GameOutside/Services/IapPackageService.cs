using ChillyRoom.Functions.DBModel;
using GameExternal;
using GameOutside.DBContext;
using GameOutside.Models;
using GameOutside.Repositories;

namespace GameOutside.Services;

public class IapPackageService(IIapPackageRepository iapPackageRepository, BuildingGameDB dbCtx)
{
    public void AddPromotionData(long playerId, short shardId)
    {
        iapPackageRepository.AddPromotionData(playerId, shardId);
    }

    public async Task<UserPaymentAndPromotionStatus?> GetPromotionData(long playerId, short? shardId)
    {
        return await dbCtx.WithDefaultRetry(_ => iapPackageRepository.GetPromotionData(playerId, shardId));
    }

    public async Task<int> GetIapPurchaseCountWithinTimeAsync(
        short shardId, long playerId, string propId, IapCommodityConfig iapConfig, long timeZoneOffset)
    {
        return await dbCtx.WithDefaultRetry(_ =>
            iapPackageRepository.GetIapPurchaseCountWithinTimeAsync(
                shardId, playerId, propId, iapConfig, timeZoneOffset));
    }

    public async Task<List<UserIapPurchaseRecord>> GetFullPurchaseListAsync(short shardId, long playerId)
    {
        return await dbCtx.WithDefaultRetry(_ => iapPackageRepository.GetFullPurchaseListAsync(shardId, playerId));
    }

    public void AddIapPurchaseRecord(short shardId, long playerId, string propId)
    {
        iapPackageRepository.AddIapPurchaseRecord(shardId, playerId, propId);
    }
}