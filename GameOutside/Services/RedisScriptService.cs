using StackExchange.Redis;

namespace GameOutside.Services;

/// <summary>
/// Redis Lua 脚本管理服务，用于在应用启动时预加载脚本到 Redis
/// 使用分布式锁，不考虑数据库冲突
/// </summary>
public interface IRedisScriptService
{
    /// <summary>
    /// 获取用户分组分配脚本的 SHA1 哈希值
    /// </summary>
    ValueTask<LoadedLuaScript> GetUserGroupAllocationScriptAsync();
}

public class RedisScriptService(
    IConnectionMultiplexer redisConn) : IRedisScriptService
{
    private const string ScriptName = "UserGroupAllocation";

    // 用户分组分配 Lua 脚本
    // @counter_prefix: 基础计数器键前缀，rank_group:{seasonNumber}:{divisionNumber}:counter，会拼接 groupId 为 rank_group:{seasonNumber}:{divisionNumber}:counter:{groupId} 作为分组计数器的 key
    // @group_key: 当前分组ID键，rank_group:{seasonNumber}:{divisionNumber}:group_id
    // @max_size: 最大分组大小
    // @expire_time: 键的有效期（秒）
    // 返回值: 分配到的分组ID
    private static readonly LuaScript UserGroupAllocationScript = LuaScript.Prepare(@"
        local counter_prefix = @counter_prefix
        local group_key = @group_key
        local max_size = tonumber(@max_size)
        local expire_time = tonumber(@expire_time)
        
        -- 获取当前分组ID
        local current_group_id = redis.call('GET', group_key)
        if not current_group_id then
            current_group_id = '0'
            -- 第一次创建分组时，需要设置group_key为0
            redis.call('SETEX', group_key, expire_time, current_group_id)
        end

        -- 获取当前分组计数
        local current_counter_key = counter_prefix .. ':' .. current_group_id
        local current_count = redis.call('GET', current_counter_key)
        if not current_count then
            current_count = 0
            -- 无需初始化，INCR 会自动创建
        else
            current_count = tonumber(current_count)
        end

        -- 如果当前分组已满，创建新分组
        if current_count >= max_size then
            local new_group_id = tonumber(current_group_id) + 1
            redis.call('SETEX', group_key, expire_time, new_group_id)
            redis.call('SETEX', counter_prefix .. ':' .. new_group_id, expire_time, 1)
            return new_group_id
        else
            -- 当前分组未满，增加计数
            redis.call('INCR', current_counter_key)
            return tonumber(current_group_id)
        end
    ");

    private LoadedLuaScript? UserGroupAllocationLoadedScript { get; set; }

    public async ValueTask<LoadedLuaScript> GetUserGroupAllocationScriptAsync()
    {
        if (UserGroupAllocationLoadedScript is not null)
        {
            return UserGroupAllocationLoadedScript;
        }
        return await UserGroupAllocationScript.LoadAsync(GetRedisMaster());
    }

    private IServer GetRedisMaster()
    {
        var endpoints = redisConn.GetEndPoints();
        foreach (var endpoint in endpoints)
        {
            var server = redisConn.GetServer(endpoint);
            if (!server.IsReplica)
            {
                return server;
            }
        }
        throw new Exception("No Redis master server found");
    }
}
