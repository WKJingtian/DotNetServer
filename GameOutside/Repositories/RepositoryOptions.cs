namespace GameOutside.Repositories;

public enum StaleReadOptions
{
    /// <summary>
    /// 允许从数据库非主节点返回一个略早于当前时间（默认约4.8s）的时间戳的数据，以提升性能，不适合用在更新数据的场景
    /// </summary>
    AllowStaleRead,
    /// <summary>
    /// 允许从数据库非主节点返回一个较早的时间戳的数据（15s，大于配置数据库超时时间10s），以提升性能，不适合用在更新数据的场景
    /// </summary>
    Allow15sStaleRead,
    /// <summary>
    /// 不允许读取过期数据
    /// </summary>
    NoStaleRead
}
