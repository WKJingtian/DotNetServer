using System.Text.Json.Serialization;
using AlibabaCloud.TairSDK;
using AlibabaCloud.TairSDK.TairZset;
using GameOutside.Repositories;
using GameOutside.Util;
using StackExchange.Redis;
using System.Diagnostics;

public class PlayerRank
{
    [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
    public long PlayerId { get; set; }

    [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
    public long Score { get; set; }
}

public class LeaderboardModule(
    [FromKeyedServices("GlobalCache")] IConnectionMultiplexer globalCache,
    ISeasonInfoRepository seasonInfoRepository,
    ILogger<LeaderboardModule> logger
    )
{
    public static readonly string NormalModeLeaderBoardId = "leader_normal";
    public static readonly string SurvivorModeLeaderBoardId = "leader_survivor";
    public static readonly string TowerDefenceModeLeaderBoardId = "leader_tower_defence";
    public static readonly string TrueEndlessModeLeaderBoardId = "leader_true_endless";

    /// <summary>
    /// 获取排行榜的键名，如果赛季号为null，则使用当前赛季号
    /// </summary>
    private string GetLeaderBoardKey(string leaderboardId, int? season = null)
    {
        season ??= seasonInfoRepository.GetCurrentSeasonNumber();

        return $"{leaderboardId}_{season}";
    }

    /// <summary>
    /// 查询排行榜前n位的角色ID
    /// </summary>
    /// <returns>前n的角色ID，数组元素可能少于n</returns>
    public async ValueTask<PlayerRank[]> GetTopPlayers(int startInclusive, int endExclusive, string leaderboardId, int? season = null)
    {
        if (startInclusive < 0 || endExclusive < startInclusive)
        {
            throw new ArgumentException("排名范围有误");
        }
        if (startInclusive == endExclusive)
        {
            return [];
        }

        var db = globalCache.GetDatabase();

        var key = GetLeaderBoardKey(leaderboardId, season);
        var topPlayers = await db.EXZRevRangeWithScores(key, startInclusive, endExclusive - 1);
        if (topPlayers is null || topPlayers.Count == 0)
        {
            return [];
        }

        var result = new PlayerRank[topPlayers.Count / 2];
        for (int i = 0; i < topPlayers.Count; i += 2)
        {
            var scores = topPlayers[i + 1].Split('#');
            result[i / 2] = new PlayerRank
            {
                PlayerId = long.Parse(topPlayers[i]),
                Score = (long)double.Parse(scores[0]),
            };
        }
        return result;
    }

    public async ValueTask<int> GetLeaderBoardCardinality(string leaderboardId, int? season = null)
    {
        var db = globalCache.GetDatabase();

        var key = GetLeaderBoardKey(leaderboardId, season);
        return (int)await db.EXZCard(key);
    }

    private readonly int MaxRank = 10000;

    /// <summary>
    /// 查询角色在排行榜上排位
    /// </summary>
    /// <returns>榜上排名，若角色未上榜，返回-1</returns>
    public async ValueTask<int> GetPlayerRank(long playerId, string leaderboardId, int? season = null)
    {
        var db = new TairZsetAsync(globalCache as ConnectionMultiplexer, -1);

        var key = GetLeaderBoardKey(leaderboardId, season);
        var rank = await db.exzrevrank(key, playerId.ToString());
        if (rank.IsNull)
        {
            return -1;
        }

        var rankValue = ResultHelper.Long(rank);
        if (rankValue >= MaxRank)
        {
            return -1; // 玩家未上榜
        }

        return (int)ResultHelper.Long(rank) + 1;
    }

    /// <summary>
    /// 查询分数在排行榜中的百分比
    /// </summary>
    /// <param name="score"></param>
    /// <param name="leaderboardId"></param>
    /// <param name="season"></param>
    /// <returns> 返回 0 - 1的百分比, 0 最高，1最低 </returns>
    /// <exception cref="ArgumentException"></exception>
    public async ValueTask<float> GetPlayerRankPercentage(long score, string leaderboardId, int? season = null)
    {
        if (score <= 0)
            return -1;
        var redisDb = globalCache.GetDatabase();
        var key = GetLeaderBoardKey(leaderboardId, season);
        var distributionId = GetDistributionId(key);
        var buckets = await redisDb.HashGetAllAsync(distributionId);
        if (buckets.Length == 0)
            return 1;
        var bucket = CalculateScoreBucket(score);
        var total = buckets.Sum(x => Math.Max((int)x.Value, 0));
        var rank = buckets.Where(x => (int)x.Name > bucket).Sum(x => Math.Max((int)x.Value, 0));
        return (float)rank / total;
    }


    // 12.4/5 赛季
    private const int _minSeasonForScoreTimestamp = 77; 
    
    /// <summary>
    /// 更新排行榜分数
    /// </summary>
    /// <returns></returns>
    public async ValueTask UpdateScore(
        long playerId,
        long score,
        long oldScore,
        long timeStamp,
        string leaderboardId,
        int seasonNumber)
    {
        if (score <= 0)
        {
            throw new ArgumentException("score需要大于0");
        }
        
        var key = GetLeaderBoardKey(leaderboardId, seasonNumber);
        var db = new TairZsetAsync(globalCache as ConnectionMultiplexer, -1);
        // 需要保证同一个榜上的score数量相同,青铜榜就不管了
        // 12.2/3 赛季
        if (seasonNumber >= _minSeasonForScoreTimestamp)
            await db.exzadd(key, playerId.ToString(), score, -timeStamp);
        else
            await db.exzadd(key, playerId.ToString(), score);

        // 只保留排名最高的部分，随机化删除以优化性能
        if (Random.Shared.NextDouble() < 0.02)
        {
            await db.exzremrangeByRank(key, 0, -MaxRank);
        }

        // 维护关卡分布
        var distributionId = GetDistributionId(key);
        var oldBucket = CalculateScoreBucket(oldScore);
        var bucket = CalculateScoreBucket(score);
        if (oldBucket == bucket)
            return;
        var redisDb = globalCache.GetDatabase();
        if (oldBucket != bucket && oldBucket > 0)
            await redisDb.HashDecrementAsync(distributionId, oldBucket);
        await redisDb.HashIncrementAsync(distributionId, bucket);
        redisDb.KeyExpireAsync(distributionId, TimeSpan.FromDays(180), flags: CommandFlags.FireAndForget);
    }

    /// <summary>
    /// 移除角色排行榜数据
    /// </summary>
    /// <returns></returns>
    public async ValueTask RemovePlayerFromLeaderBoard(long playerId, string leaderboardId, int? seasonNumber = null)
    {
        var db = new TairZsetAsync(globalCache as ConnectionMultiplexer, -1);
        var key = GetLeaderBoardKey(leaderboardId, seasonNumber);
        await db.exzrem(key, playerId.ToString());

        // 需要同时维护分布
        var scoreResult = await db.exzscore(key, playerId.ToString());
        if (scoreResult.IsNull)
            return;
        if (!double.TryParse(scoreResult.ToString(), out var scoreDouble))
            return;
        long score = (long)scoreDouble;
        var distributionId = GetDistributionId(key);
        var bucket = CalculateScoreBucket(score);
        var redisDb = globalCache.GetDatabase();
        if (bucket > 0)
            await redisDb.HashDecrementAsync(distributionId, bucket);
    }

    /// <summary>
    /// 清空排行榜，保留参数指定的多个赛季号的数据，清除其他赛季号的所有用户排名数据
    /// </summary>
    public async ValueTask ClearLeaderBoard(string leaderboardId, HashSet<int> seasonNumbersToBeKept)
    {
        var sw = Stopwatch.StartNew();
        // 限制清理的最大赛季号，防止结算时已到下个赛季的极端情况
        int maxSeasonNumberToBeKept = seasonNumbersToBeKept.Max();

        try
        {
            var redisMaster = globalCache.GetRedisMasterServer();
            var redis = globalCache.GetDatabase();

            // 使用 SCAN 遍历所有排行榜相关的 keys
            var pattern = $"{leaderboardId}_*";
            var totalDeletedKeys = 0;
            var seasonKeysDeletedCount = new Dictionary<int, int>();

            await foreach (var key in redisMaster.KeysAsync(pattern: pattern))
            {
                // 解析 key 格式: {leaderboardId}_{seasonNumber} 或 {leaderboardId}_{seasonNumber}_dist
                var keyString = key.ToString();
                var keyParts = keyString.Split('_');

                if (keyParts.Length < 2)
                {
                    logger.LogError("Invalid leaderboard key format: {Key}", key);
                    continue; // 无效的 key 格式，跳过
                }

                // 提取赛季号（倒数第二个部分，因为可能有 _dist 后缀）
                var seasonNumberPart = keyParts[^1] == "dist" ? keyParts[^2] : keyParts[^1];

                if (!int.TryParse(seasonNumberPart, out var seasonNumber))
                {
                    logger.LogError("Failed to parse season number from leaderboard key: {Key}", key);
                    continue; // 赛季号解析失败，跳过
                }

                // 如果赛季号不在保留列表中且赛季号小于等于最大保留赛季号，则删除
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
                        logger.LogInformation("Cleared {KeyCount} leaderboard keys so far", totalDeletedKeys);
                        await Task.Delay(5);
                    }
                }
            }

            foreach (var kvp in seasonKeysDeletedCount)
            {
                logger.LogInformation("Leaderboard {LeaderboardId} Season {SeasonNumber} cleared {KeyCount} keys",
                    leaderboardId, kvp.Key, kvp.Value);
            }
            sw.Stop();
            logger.LogInformation("Total cleared {TotalKeyCount} leaderboard keys for {LeaderboardId}, elapsed time: {ElapsedMilliseconds} ms",
                totalDeletedKeys, leaderboardId, sw.ElapsedMilliseconds);
        }
        catch (Exception e)
        {
            sw.Stop();
            logger.LogError(e, "Error clearing leaderboard {LeaderboardId}, kept seasons: {KeptSeasons}, elapsed time: {ElapsedMilliseconds} ms",
                leaderboardId, string.Join(", ", seasonNumbersToBeKept), sw.ElapsedMilliseconds);
            throw;
        }
    }

    private int CalculateScoreBucket(long score)
    {
        // C4测试分数分布统计（近似）：
        // <52300             : 10%
        // 52300 - 57800      : 10%
        // 57800 - 67200      : 10%
        // 67200 - 86200      : 10%
        // 86200 - 118500     : 10%
        // 118500 - 162500    : 10%
        // 162500 - 222000    : 10%
        // 222000 - 307000    : 10%
        // 307000 - 580000    : 10%
        // 580000 - 1000000   : 4%
        // 1000000 - 5000000  : 5%
        // 5000000 - 10000000 : 1%
        // >10000000          : 1% (直接1000W一档）
        if (score < 52300)
            return (int)(score / 5230);
        if (score < 57800)
            return 9 + (int)((score - 52300) / 550);
        if (score < 67200)
            return 19 + (int)((score - 57800) / 940);
        if (score < 86200)
            return 29 + (int)((score - 67200) / 1900);
        if (score < 118500)
            return 39 + (int)((score - 86200) / 3230);
        if (score < 162500)
            return 49 + (int)((score - 118500) / 4400);
        if (score < 222000)
            return 59 + (int)((score - 162500) / 5950);
        if (score < 307000)
            return 69 + (int)((score - 222000) / 8500);
        if (score < 580000)
            return 79 + (int)((score - 307000) / 27300);
        if (score < 1000000)
            return 89 + (int)((score - 580000) / 105000);
        if (score < 5000000)
            return 93 + (int)((score - 1000000) / 1000000);
        if (score < 10000000)
            return 98;
        return (int)(score / 10000000) + 99;
    }

    private static string GetDistributionId(string leaderboardKey)
    {
        return leaderboardKey + "_dist";
    }
}

static class RedisExtension
{
    public static async Task<List<string>> EXZRevRangeWithScores(this IDatabase db, string key, long min, long max, CommandFlags flags = CommandFlags.None)
    {
        var args = new object[]
        {
            key, min, max, "WITHSCORES"
        };

        return ResultHelper.ListString(await db.ExecuteAsync(ModuleCommand.EXZREVRANGE, args, flags));
    }

    public static async Task<long> EXZCard(this IDatabase db, string key, CommandFlags flags = CommandFlags.None)
    {
        var args = new object[]
        {
            key
        };

        return ResultHelper.Long(await db.ExecuteAsync(ModuleCommand.EXZCARD, args, flags));
    }
}
