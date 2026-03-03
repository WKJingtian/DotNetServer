using System.Text.Json;
using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Infra.ApiController;
using GameOutside.Controllers;
using GameOutside.Util;
using StackExchange.Redis;

namespace GameOutside.Services.PlatformItemsService;

public partial class PlatformItemsService
{
    public ValueTask SaveMailItemsCacheAsync(long playerId, string payload) =>
        SaveItemsCacheAsync(CacheKey.MailItems(playerId), payload);

    public ValueTask SaveGiftCodeItemsCacheAsync(long playerId, string payload) =>
        SaveItemsCacheAsync(CacheKey.GiftCodeItems(playerId), payload);

    /// <summary>
    /// 长连接消息不一定送达，将到货物品信息存入缓存，用于主动轮询时弹窗显示物品信息
    /// </summary>
    /// <param name="cacheKey"></param>
    /// <param name="payload">序列化后的附件</param>
    private async ValueTask SaveItemsCacheAsync(string cacheKey, string payload)
    {
        try
        {
            var redisDb = redisConn.GetDatabase();
            await redisDb.ListRightPushAsync(cacheKey, payload);
            await redisDb.KeyExpireAsync(cacheKey, TimeSpan.FromDays(7), flags: CommandFlags.FireAndForget);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "附件插入缓存失败");
        }
    }

    public Task<ClaimAttachmentMessage[]> ClaimMailItemsCacheAsync(long playerId) =>
        ClaimItemsCacheAsync<ClaimAttachmentMessage>(CacheKey.MailItems(playerId));

    public Task<ClaimAttachmentMessage[]> ClaimGiftCodeItemsCacheAsync(long playerId) =>
        ClaimItemsCacheAsync<ClaimAttachmentMessage>(CacheKey.GiftCodeItems(playerId));

    public static readonly ErrorResponse ClaimCacheErrorResponse = new()
    {
        ErrorCode = (int)ErrorKind.CLAIM_CACHE_ERROR,
        Message = "Failed to claim items"
    };

    private async Task<T[]> ClaimItemsCacheAsync<T>(string cacheKey)
    {
        var cache = await redisConn.GetDatabase().ListLeftPopAsync(cacheKey, 100);
        if (cache is null || cache.Length == 0)
        {
            return [];
        }

        return cache.Select(v => JsonSerializer.Deserialize<T>(v.ToString())!).ToArray();
    }
}