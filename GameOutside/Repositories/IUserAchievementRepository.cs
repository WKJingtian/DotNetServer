using System.Text;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.DBContext;
using GameOutside.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace GameOutside.Repositories;

public interface IUserAchievementRepository
{
    public Task<List<UserAchievement>> GetUserAchievements(short shardId, long playerId, TrackingOptions trackingOptions);
    public Task<UserAchievement?> GetUserAchievementInfo(short shardId, long playerId, int configId, string target,
        TrackingOptions trackingOptions);
    public Task UpsertUserAchievementsAsync(IEnumerable<UserAchievement> userAchievements);
    public Task<List<UserAchievement>> IncreaseAchievementProgressAsync(
        List<AchievementRecord> records,
        short shardId,
        long playerId);
    public Task<List<UserAchievement>> UpdateAchievementProgressAsync(
        List<AchievementRecord> records,
        short shardId,
        long playerId);
}

public class UserAchievementRepository(BuildingGameDB dbCtx,
    ServerConfigService serverConfigService) : IUserAchievementRepository
{
    /// <summary>
    /// 应用查询配置选项
    /// </summary>
    private static IQueryable<UserAchievement> ApplyQueryOptions<T>(
        IQueryable<T> query,
        TrackingOptions trackingOptions) where T : UserAchievement
    {
        var typedQuery = query.Cast<UserAchievement>();

        if (trackingOptions == TrackingOptions.NoTracking)
        {
            typedQuery = typedQuery.AsNoTracking();
        }

        return typedQuery;
    }

    public Task<List<UserAchievement>> GetUserAchievements(short shardId, long playerId, TrackingOptions trackingOptions)
    {
        var query = dbCtx.UserAchievements
                .Where(achievement => achievement.PlayerId == playerId && achievement.ShardId == shardId);
        query = ApplyQueryOptions(query, trackingOptions);
        return query.ToListAsync();
    }

    public Task<UserAchievement?> GetUserAchievementInfo(
        short shardId,
        long playerId,
        int configId,
        string target,
        TrackingOptions trackingOptions)
    {
        var query = dbCtx.UserAchievements.AsQueryable();
        query = ApplyQueryOptions(query, trackingOptions);
        return query.FirstOrDefaultAsync(achievement =>
            achievement.PlayerId == playerId &&
            achievement.ShardId == shardId &&
            achievement.ConfigId == configId &&
            achievement.Target == target);
    }

    public Task UpsertUserAchievementsAsync(IEnumerable<UserAchievement> userAchievements)
    {
        return dbCtx.UserAchievements.UpsertRange(userAchievements).RunAsync();
    }

    public Task<List<UserAchievement>> IncreaseAchievementProgressAsync(
        List<AchievementRecord> records,
        short shardId,
        long playerId)
    {
        return UpsertAchievementsAsync(
            records,
            shardId,
            playerId,
            static (current, value) => current + value,
            additiveMode: true);
    }

    /// <summary>
    /// 覆盖更新成就进度
    /// </summary>
    /// <param name="records">当前如果 records 中存在相同的成就记录，则以靠后的记录为准，TODO: 增加参数安全性</param>
    public Task<List<UserAchievement>> UpdateAchievementProgressAsync(
        List<AchievementRecord> records,
        short shardId,
        long playerId)
    {
        return UpsertAchievementsAsync(
            records,
            shardId,
            playerId,
            static (_, value) => value,
            additiveMode: false);
    }

    /// <summary>
    /// 批量更新/插入成就
    /// </summary>
    /// <param name="records">成就增量/覆盖值，聚合策略由 <paramref name="recordAccumulator"/> 决定。</param>
    /// <param name="recordAccumulator">定义相同键的多条记录如何聚合</param>
    /// <param name="additiveMode">为 true 时表示服务器侧在冲突时累加进度，否则直接覆盖。</param>
    private async Task<List<UserAchievement>> UpsertAchievementsAsync(
        List<AchievementRecord> records,
        short shardId,
        long playerId,
        Func<int, int, int> recordAccumulator,
        bool additiveMode)
    {
        var (keys, aggregated) = AggregateAchievementRecords(records, recordAccumulator);
        if (aggregated.Count == 0)
            return [];

        return await ExecuteUpsertAsync(keys, aggregated, shardId, playerId, additiveMode);
    }

    /// <summary>
    /// 按 configId 和 target 对原始成就记录去重
    /// </summary>
    /// <param name="records">需要聚合的原始成就记录</param>
    /// <param name="accumulator">重复键的聚合策略</param>
    /// <returns>（ConfigId, Target）集合及对应的聚合进度增量。</returns>
    private (List<(int ConfigId, string Target)> Keys, Dictionary<(int ConfigId, string Target), int> Aggregated)
        AggregateAchievementRecords(
            IEnumerable<AchievementRecord> records,
            Func<int, int, int> accumulator)
    {
        var aggregated = new Dictionary<(int ConfigId, string Target), int>();
        var keys = new List<(int ConfigId, string Target)>();

        foreach (var record in records)
        {
            var config = serverConfigService.GetAchievementConfigByKey(record.Key, record.Target);
            if (config == null)
                continue;

            var key = (config.id, record.Target);
            if (aggregated.TryGetValue(key, out var current))
            {
                aggregated[key] = accumulator(current, record.Count);
            }
            else
            {
                aggregated[key] = accumulator(0, record.Count);
                keys.Add(key);
            }
        }

        return (keys, aggregated);
    }

    private async Task<List<UserAchievement>> ExecuteUpsertAsync(
        List<(int ConfigId, string Target)> keys,
        IReadOnlyDictionary<(int ConfigId, string Target), int> aggregated,
        short shardId,
        long playerId,
        bool additiveMode)
    {
        var sqlBuilder = new StringBuilder();
        sqlBuilder.Append($"INSERT INTO \"UserAchievements\" AS ua (\"ShardId\", \"PlayerId\", \"ConfigId\", \"Target\", \"CurrentIndex\", \"Progress\", \"Received\") VALUES ");

        var parameters = new List<NpgsqlParameter>(keys.Count * 7);

        string AddParameter(object value, NpgsqlDbType? dbType = null)
        {
            var parameterName = $"p{parameters.Count}";
            var parameter = new NpgsqlParameter(parameterName, value);
            if (dbType.HasValue)
                parameter.NpgsqlDbType = dbType.Value;
            parameters.Add(parameter);
            return $"@{parameterName}";
        }

        for (var i = 0; i < keys.Count; i++)
        {
            if (i > 0)
                sqlBuilder.Append(", ");

            var key = keys[i];
            var aggregatedProgress = aggregated[key];

            sqlBuilder.Append('(');
            sqlBuilder.Append(AddParameter(shardId));
            sqlBuilder.Append(", ");
            sqlBuilder.Append(AddParameter(playerId));
            sqlBuilder.Append(", ");
            sqlBuilder.Append(AddParameter(key.ConfigId));
            sqlBuilder.Append(", ");
            sqlBuilder.Append(AddParameter(key.Target));
            sqlBuilder.Append(", ");
            sqlBuilder.Append(AddParameter(0));
            sqlBuilder.Append(", ");
            sqlBuilder.Append(AddParameter(aggregatedProgress));
            sqlBuilder.Append(", ");
            sqlBuilder.Append(AddParameter(false, NpgsqlDbType.Boolean));
            sqlBuilder.Append(')');
        }

        sqlBuilder.Append(" ON CONFLICT (\"PlayerId\", \"ConfigId\", \"Target\", \"ShardId\") DO UPDATE SET \"Progress\" = ");
        sqlBuilder.Append(additiveMode ? "ua.\"Progress\" + EXCLUDED.\"Progress\"" : "EXCLUDED.\"Progress\"");
        const string returningColumns = "\"ShardId\", \"PlayerId\", \"ConfigId\", \"Target\", \"CurrentIndex\", \"Progress\", \"Received\"";
        sqlBuilder.Append($" RETURNING {returningColumns}");

        var upsertSql = sqlBuilder.ToString();
        var finalSql = $"WITH upsert AS ({upsertSql}) SELECT {returningColumns} FROM upsert";

        return await dbCtx.UserAchievements
            .FromSqlRaw(finalSql, [.. parameters.Cast<object>()])
            .AsNoTracking()
            .ToListAsync();
    }
}
