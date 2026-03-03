using StackExchange.Redis;
using GameOutside.Services;
using System.Diagnostics;
using GameOutside.Util;
using Grpc.Net.ClientFactory;
using Polly;
using Grpc.Core;
using System.Collections.Immutable;
using ChillyRoom.BuildingGame.v1;

namespace GameOutside.Repositories;

/// <summary>
/// 这是一个基于 redis （Global Cache）的玩家分组模块，数据层级为赛季（避免赛季更迭时的竞态条件问题）->段位->分组，
/// 玩家通过该接口可以获取一个自己所在分组的 GroupID，GroupID 从0开始递增，每个分组会准确地以配置的分组最大人数为准，不会多人或者少人
/// </summary>
public interface IUserRankGroupRepository
{
    /// <summary>
    /// 从某赛季的某段位获取一个分组 ID
    /// </summary>
    /// <param name="seasonNumber">赛季号</param>
    /// <param name="divisionNumber">段位号</param>
    /// <param name="playerShardId">玩家存档的 ShardId，分组需在存档所在的 Shard 进行</param>
    /// <returns>分组 ID</returns>
    ValueTask<int> GetGroupIdAsync(int seasonNumber, int divisionNumber, short playerShardId);
    /// <summary>
    /// 从本集群某赛季的某段位获取一个分组 ID
    /// </summary>
    /// <param name="seasonNumber">赛季号</param>
    /// <param name="divisionNumber">段位号</param>
    /// <returns>分组 ID</returns>
    ValueTask<int> GetLocalGroupIdAsync(int seasonNumber, int divisionNumber);
    /// <summary>
    /// 获取某赛季的某段位的所有分组 ID
    /// </summary>
    /// <param name="seasonNumber">赛季号</param>
    /// <param name="divisionNumber">段位号</param>
    /// <returns>所有分组 ID</returns>
    ValueTask<IEnumerable<int>> GetAllLocalGroupIdsAsync(int seasonNumber, int divisionNumber);

    /// <summary>
    /// 清空分组相关数据，保留参数指定的多个赛季号的数据，清除其他赛季号的所有数据
    /// </summary>
    ValueTask ClearLocalGroupIdsAsync(HashSet<int> seasonNumbersToBeKept);
}

public class UserRankGroupRepository(
    IConnectionMultiplexer redisConn,
    ILogger<UserRankGroupRepository> logger,
    ServerConfigService serverConfigService,
    IRedisScriptService redisScriptService,
    GrpcClientFactory grpcClientFactory) : IUserRankGroupRepository
{
    private const int RedisKeyExpireTimeInSeconds = 3600 * 24 * 30; // 设置一个月的有效期
    /// <summary>
    /// 从本集群某赛季的某段位获取一个分组 ID
    /// </summary>
    /// <param name="seasonNumber">赛季号</param>
    /// <param name="divisionNumber">段位号</param>
    /// <returns>分组 ID</returns>
    public async ValueTask<int> GetLocalGroupIdAsync(int seasonNumber, int divisionNumber)
    {
        var keyPrefix = $"rank_group:{seasonNumber}:{divisionNumber}";

        try
        {
            var redis = redisConn.GetDatabase();
            var userGroupAllocationScript = await redisScriptService.GetUserGroupAllocationScriptAsync();

            var result = await userGroupAllocationScript.EvaluateAsync(
                redis,
                new
                {
                    counter_prefix = (RedisKey)$"{keyPrefix}:counter",
                    group_key = (RedisKey)$"{keyPrefix}:group_id",
                    max_size = serverConfigService.GetRankPopulationCap(divisionNumber),
                    expire_time = RedisKeyExpireTimeInSeconds
                });

            if (result.IsNull)
            {
                logger.LogError("Failed to get group ID from Redis script");
                return 0;
            }

            return (int)result;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error getting group ID for season {SeasonNumber}, division {DivisionNumber}",
                seasonNumber, divisionNumber);
            throw;
        }
    }

    private readonly ImmutableDictionary<short, string> _shardIdToRegion = new Dictionary<short, string>
    {
        { 1000, "eu-fr-aliyun" },
        { 2000, "cn-hangzhou" },
        { 3000, "ap-sg-aliyun" },
        { 5000, "us-sv" }
    }.ToImmutableDictionary();
    public ValueTask<int> GetGroupIdAsync(int seasonNumber, int divisionNumber, short playerShardId)
    {
        if (playerShardId == Consts.LocalShardId)
        {
            return GetLocalGroupIdAsync(seasonNumber, divisionNumber);
        }

        return GetRemoteGroupIdAsync(seasonNumber, divisionNumber, _shardIdToRegion[playerShardId]);
    }

    private static readonly IAsyncPolicy DefaultGrpcRetryPolicy = Policy
        .HandleInner((Func<RpcException, bool>)(e => e.StatusCode != StatusCode.NotFound))
        .WaitAndRetryAsync(2, x => TimeSpan.FromSeconds(Math.Pow(2, x)));
    private async ValueTask<int> GetRemoteGroupIdAsync(int seasonNumber, int divisionNumber, string remoteRegion)
    {
        var client = grpcClientFactory.CreateClient<Group.GroupClient>(remoteRegion);

        var reply = await DefaultGrpcRetryPolicy.ExecuteAsync(async () =>
            await client.GetGroupIdAsync(
                new GetGroupIdRequest
                {
                    SeasonNumber = seasonNumber,
                    DivisionNumber = divisionNumber
                }));
        return reply.GroupId;
    }

    /// <summary>
    /// 获取本集群某赛季的某段位的所有分组 ID
    /// </summary>
    /// <param name="seasonNumber">赛季号</param>
    /// <param name="divisionNumber">段位号</param>
    /// <returns>所有分组 ID</returns>
    public async ValueTask<IEnumerable<int>> GetAllLocalGroupIdsAsync(int seasonNumber, int divisionNumber)
    {
        var groupIdKey = $"rank_group:{seasonNumber}:{divisionNumber}:group_id";

        try
        {
            var redis = redisConn.GetDatabase();
            var maxGroupId = await redis.StringGetAsync(groupIdKey);

            if (!maxGroupId.HasValue)
            {
                return [];
            }

            return Enumerable.Range(0, (int)maxGroupId + 1);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error getting all group IDs for season {SeasonNumber}, division {DivisionNumber}",
                seasonNumber, divisionNumber);
            throw;
        }
    }

    /// <summary>
    /// 清空分组相关数据，保留参数指定的多个赛季号的数据，清除其他赛季号的所有数据
    /// </summary>
    public async ValueTask ClearLocalGroupIdsAsync(HashSet<int> seasonNumbersToBeKept)
    {
        var sw = Stopwatch.StartNew();
        // 限制清理的最大赛季号，防止结算时已到下个赛季的极端情况
        int maxSeasonNumberToBeKept = seasonNumbersToBeKept.Max();

        try
        {
            var redisMaster = redisConn.GetRedisMasterServer();
            var redis = redisConn.GetDatabase();

            // 使用 SCAN 遍历所有 rank_group 相关的 keys
            var pattern = "rank_group:*";
            var totalDeletedKeys = 0;
            var seasonKeysDeletedCount = new Dictionary<int, int>();

            await foreach (var key in redisMaster.KeysAsync(pattern: pattern))
            {
                // 解析 key 格式: rank_group:{seasonNumber}:{divisionNumber}:...
                var keyParts = key.ToString().Split(':');
                if (keyParts.Length < 2)
                {
                    logger.LogError("Invalid key format: {Key}", key);
                    continue; // 无效的 key 格式，跳过
                }
                if (!int.TryParse(keyParts[1], out var seasonNumber))
                {
                    logger.LogError("Failed to parse season number from key: {Key}", key);
                    continue; // 赛季号解析失败，跳过
                }

                // 如果赛季号不在保留列表中或赛季号小于等于最大保留赛季号，则删除
                if (!seasonNumbersToBeKept.Contains(seasonNumber) && seasonNumber <= maxSeasonNumberToBeKept)
                {
                    await redis.KeyDeleteAsync(key);
                    if (!seasonKeysDeletedCount.ContainsKey(seasonNumber))
                    {
                        seasonKeysDeletedCount[seasonNumber] = 0;
                    }
                    seasonKeysDeletedCount[seasonNumber]++;
                    totalDeletedKeys++;

                    // 每删除100个key后延迟一下，避免Redis负载过高
                    if (totalDeletedKeys % 100 == 0)
                    {
                        logger.LogInformation("Cleared {KeyCount} keys so far", totalDeletedKeys);
                        await Task.Delay(5);
                    }
                }
            }

            foreach (var kvp in seasonKeysDeletedCount)
            {
                logger.LogInformation("Season {SeasonNumber} cleared {KeyCount} keys",
                    kvp.Key, kvp.Value);
            }
            sw.Stop();
            logger.LogInformation("Total cleared {TotalKeyCount} keys, elapsed time: {ElapsedMilliseconds} ms",
                totalDeletedKeys, sw.ElapsedMilliseconds);
        }
        catch (Exception e)
        {
            sw.Stop();
            logger.LogError(e, "Error clearing group IDs, kept seasons: {KeptSeasons}, elapsed time: {ElapsedMilliseconds} ms",
                string.Join(", ", seasonNumbersToBeKept), sw.ElapsedMilliseconds);
            throw;
        }
    }
}
