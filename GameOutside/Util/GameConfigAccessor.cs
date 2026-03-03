using System;
using System.Collections.Generic;
using System.Linq;
using GameOutside;

/// <summary>
/// 配置访问器，简单地包装一下
/// 适用于id连续的配置信息
/// </summary>
/// <typeparam name="T"></typeparam>
public class GameConfigAccessor<T> where T : class
{

    public GameConfigAccessor(string jsonFileName, ServerConfigService serverConfigService)
    {
        ConfigList = serverConfigService.GetConfigList<T>(jsonFileName);
    }

    public List<T> ConfigList { get; }

    public T? this[int id] => id >= 0 && id < ConfigList.Count ? ConfigList[id] : null;
}


/// <summary>
/// key-config封装
/// 同时包含了原始配置
/// </summary>
/// <typeparam name="T"></typeparam>
public class GameConfigByKeyAccessor<T> : GameConfigAccessor<T> where T : class
{

    public GameConfigByKeyAccessor(
        string jsonFileName,
        Func<T, string> keyFunc,
        ServerConfigService serverConfigService) : base(jsonFileName, serverConfigService)
    {
        ConfigByKey = ConfigList.ToDictionary(keyFunc);
    }

    public Dictionary<string, T> ConfigByKey { get; }

    public T? this[string key] => ConfigByKey.TryGetValue(key, out T? config) ? config : null;
}

/// <summary>
/// id-config包装
/// 适用于id不连续但需要通过id来访问的配置
/// </summary>
/// <typeparam name="T"></typeparam>
public class GameConfigByIdAccessor<T> : GameConfigAccessor<T> where T : class
{
    public GameConfigByIdAccessor(string jsonFileName, Func<T, int> keyFunc, ServerConfigService serverConfigService) :
        base(jsonFileName, serverConfigService)
    {
        ConfigById = ConfigList.ToDictionary(keyFunc);
    }

    public Dictionary<int, T> ConfigById { get; }

    public new T? this[int id] => ConfigById.TryGetValue(id, out var config) ? config : null;
}