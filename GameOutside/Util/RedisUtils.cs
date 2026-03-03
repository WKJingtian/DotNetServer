using StackExchange.Redis;

namespace GameOutside.Util;

public static class RedisUtils
{
    public static IServer GetRedisMasterServer(this IConnectionMultiplexer redisConn)
    {
        return redisConn.GetEndPoints()
            .Select(endpoint => redisConn.GetServer(endpoint))
            .FirstOrDefault(server => !server.IsReplica)
            ?? throw new InvalidOperationException("No master Redis server found.");
    }
}