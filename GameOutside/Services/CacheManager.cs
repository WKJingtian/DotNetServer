using System.Text.Encodings.Web;
using System.Text.Json;
using ChillyRoom.Infra.Extensions;
using Microsoft.Extensions.Caching.Distributed;
using NuGet.Protocol;

namespace ChillyRoom.Games.BuildingGame.Services;

public class CacheManager(
    IDistributedCache cache,
    ILogger<CacheManager> logger)
{
    private readonly DistributedCacheEntryOptions DefaultCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1),
    };

    private readonly JsonSerializerOptions _defaultJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        // TypeInfoResolver = CacheMCacheContext.Default,
    };

    public async Task<T?> GetCustomJson<T>(string cacheKey) where T : class
    {
        var result = await Get<string>(cacheKey);
        if (result == null)
            return null;
        return result.FromJson<T>();
    }

    // 获取
    private async Task<T?> Get<T>(string cacheKey) where T : class
    {
        return await cache.GetJsonAsync<T>(cacheKey, compressionType: CachingExtensions.CompressionType.Brotli);
    }

    public async Task<bool> SetCustomJson<T>(string cacheKey, T body)
    {
        return await Set<string>(cacheKey, body.ToJson());
    }

    // 设值
    public async Task<bool> Set<T>(string cacheKey, T body)
    {
        try
        {
            await cache.SetJsonAsync(cacheKey, body,
                compressionType: CachingExtensions.CompressionType.Brotli,
                jsonOptions: _defaultJsonOptions,
                cacheOptions: DefaultCacheOptions);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to set cache for {Key}", cacheKey);
            return false;
        }
    }


    public async Task<bool> SetCustomJson<T>(string cacheKey, T body, TimeSpan timeSpan)
    {
        return await Set<string>(cacheKey, body.ToJson(), timeSpan);
    }

    public async Task<bool> Set<T>(string cacheKey, T body, TimeSpan timeSpan)
    {
        try
        {
            await cache.SetJsonAsync(cacheKey, body,
                compressionType: CachingExtensions.CompressionType.Brotli,
                jsonOptions: _defaultJsonOptions,
                cacheOptions: new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = timeSpan
                });

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to set cache for {Key}", cacheKey);
            return false;
        }
    }

    // 删除
    // public async Task<bool> Delete(Guid characterId, CacheGroupType cacheGroup, int subKind)
    // {
    //     try
    //     {
    //         await Redis.RemoveAsync($"{characterId:N}_{(int)cacheGroup}_{subKind}");
    //         return true;
    //     }
    //     catch (Exception ex)
    //     {
    //         this.Logger.LogError(ex, "failed to set cache for {Key}", $"{characterId:N}_{cacheGroup}_{subKind}");
    //         return false;
    //     }
    // }

    public async Task<bool> Delete(string cacheKey)
    {
        try
        {
            await cache.RemoveAsync(cacheKey);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "failed to set cache for {Key}", cacheKey);
            return false;
        }
    }

    public async ValueTask<short?> GetPlayerGameDataShard(long playerId)
    {
        try
        {
            var shardString = await cache.GetStringAsync(CacheKey.PidShard(playerId));
            if (short.TryParse(shardString, out var shard))
            {
                return shard;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to get cache for {Key}", CacheKey.PidShard(playerId));
        }

        return null;
    }

    public async ValueTask SetPlayerGameDataShard(long playerId, short shardId)
    {
        var key = CacheKey.PidShard(playerId);
        try
        {
            await cache.SetStringAsync(key, shardId.ToString(), new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromDays(7),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30),
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to set cache for {Key}", key);
        }
    }

    public async ValueTask<long?> GetPlayerGameDataUid(long playerId)
    {
        var key = CacheKey.PidToUid(playerId);
        try
        {
            var uidString = await cache.GetStringAsync(key);
            if (long.TryParse(uidString, out var uid))
            {
                return uid;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to get cache for {Key}", key);
        }

        return null;
    }

    public async ValueTask SetPlayerGameDataUid(long playerId, long userId)
    {
        var key = CacheKey.PidToUid(playerId);
        try
        {
            await cache.SetStringAsync(key, userId.ToString(),
                new DistributedCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromDays(7),
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30),
                });
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to set cache for {Key}", key);
        }
    }

    public async ValueTask<long?> GetPidByUidAsync(long userId)
    {
        var key = CacheKey.UidToPid(userId);
        try
        {
            var pidString = await cache.GetStringAsync(key);
            if (long.TryParse(pidString, out var pid))
            {
                return pid;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to get cache for {Key}", key);
        }

        return null;
    }

    public async ValueTask SetPidByUidAsync(long userId, long playerId)
    {
        var key = CacheKey.UidToPid(userId);
        try
        {
            await cache.SetStringAsync(key, playerId.ToString(),
                new DistributedCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromDays(7),
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30),
                });
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to set cache for {Key}", key);
        }
    }

    // [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata | JsonSourceGenerationMode.Serialization)]
    // internal partial class CacheCacheContext : JsonSerializerContext
    // {
    // }

    public static class CacheKey
    {
        public static string PidShard(long playerId) => $"gds:{playerId}";
        public static string PidToUid(long playerId) => $"gduid:{playerId}";
        public static string UidToPid(long userId) => $"gpid:{userId}";
    }
}