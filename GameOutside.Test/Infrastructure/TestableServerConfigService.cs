using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace GameOutside.Test.Infrastructure;

// 用于测试的 ServerConfigService 实现
public class TestableServerConfigService : ServerConfigService
{
    private readonly Dictionary<int, int> _rankPopulationCaps = new();

    // 创建一个可测试的实例，不依赖外部配置
    public TestableServerConfigService() : base(
        new TestOptionsMonitor(),
        new TestLogger())
    {
        // 设置默认值
        SetRankPopulationCap(0, 100); // 默认分组最大人数为100

        InitializeDivisionConfig();
    }

    public void SetRankPopulationCap(int division, int populationCap)
    {
        _rankPopulationCaps[division] = populationCap;
        // 也更新内部的 _divisionConfigList
        UpdateDivisionConfig(division, populationCap);
    }

    private void InitializeDivisionConfig()
    {
        // 使用反射来初始化私有字段 _divisionConfigList
        var divisionConfigListField = typeof(ServerConfigService).GetField("_divisionConfigList",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (divisionConfigListField != null)
        {
            var divisionConfigList = divisionConfigListField.GetValue(this);
            if (divisionConfigList != null)
            {
                // 获取列表的实际类型并清空
                var clearMethod = divisionConfigList.GetType().GetMethod("Clear");
                clearMethod?.Invoke(divisionConfigList, null);

                // 添加默认的段位配置
                for (int i = 0; i <= 10; i++) // 添加足够多的段位配置
                {
                    var divisionConfig = CreateDivisionConfig(i, _rankPopulationCaps.GetValueOrDefault(i, 100));
                    var addMethod = divisionConfigList.GetType().GetMethod("Add");
                    addMethod?.Invoke(divisionConfigList, new[] { divisionConfig });
                }

                // 更新 _totalDivisionCount 字段
                var totalDivisionCountField = typeof(ServerConfigService).GetField("_totalDivisionCount",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var countProperty = divisionConfigList.GetType().GetProperty("Count");
                var divisionCount = (int)(countProperty?.GetValue(divisionConfigList) ?? 0);
                totalDivisionCountField?.SetValue(this, divisionCount);

                // 初始化 _rewardsDictionary
                InitializeRewardsDictionary(divisionCount);
            }
        }
    }

    private void InitializeRewardsDictionary(int divisionCount)
    {
        // 使用反射来初始化私有字段 _rewardsDictionary
        var rewardsDictionaryField = typeof(ServerConfigService).GetField("_rewardsDictionary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (rewardsDictionaryField != null)
        {
            var rewardsDictionary = rewardsDictionaryField.GetValue(this);
            if (rewardsDictionary != null)
            {
                // 清空现有数据
                var clearMethod = rewardsDictionary.GetType().GetMethod("Clear");
                clearMethod?.Invoke(rewardsDictionary, null);

                // 为每个段位添加奖励列表
                var addMethod = rewardsDictionary.GetType().GetMethod("Add");
                for (int i = 0; i < divisionCount; i++)
                {
                    var rewardList = new List<int>();
                    // 添加一些默认的奖励值
                    for (int j = 0; j < 10; j++)
                    {
                        rewardList.Add(100 + i * 10 + j); // 简单的奖励计算
                    }
                    addMethod?.Invoke(rewardsDictionary, new object[] { i, rewardList });
                }
            }
        }
    }

    private void UpdateDivisionConfig(int division, int populationCap)
    {
        var divisionConfigListField = typeof(ServerConfigService).GetField("_divisionConfigList",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (divisionConfigListField != null)
        {
            var divisionConfigList = divisionConfigListField.GetValue(this);
            if (divisionConfigList != null)
            {
                // 获取列表的Count属性
                var countProperty = divisionConfigList.GetType().GetProperty("Count");
                var currentCount = (int)(countProperty?.GetValue(divisionConfigList) ?? 0);

                // 确保列表足够长
                var addMethod = divisionConfigList.GetType().GetMethod("Add");
                while (currentCount <= division)
                {
                    var newConfig = CreateDivisionConfig(currentCount, 100);
                    addMethod?.Invoke(divisionConfigList, new[] { newConfig });
                    currentCount++;
                }

                // 更新指定段位的配置
                if (division < currentCount)
                {
                    var config = CreateDivisionConfig(division, populationCap);
                    var indexerProperty = divisionConfigList.GetType().GetProperty("Item");
                    indexerProperty?.SetValue(divisionConfigList, config, new object[] { division });
                }

                // 更新 _totalDivisionCount 字段
                var totalDivisionCountField = typeof(ServerConfigService).GetField("_totalDivisionCount",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var divisionCount = (int)(countProperty?.GetValue(divisionConfigList) ?? 0);
                totalDivisionCountField?.SetValue(this, divisionCount);

                // 重新初始化奖励字典以确保包含新段位
                InitializeRewardsDictionary(divisionCount);
            }
        }
    }

    private object CreateDivisionConfig(int level, int population)
    {
        // 使用反射创建 DivisionConfig 对象
        var divisionConfigType = Type.GetType("GameOutside.DivisionConfig") ??
                                Type.GetType("GameOutside.Util.DivisionConfig") ??
                                typeof(ServerConfigService).Assembly.GetTypes()
                                    .FirstOrDefault(t => t.Name == "DivisionConfig");

        if (divisionConfigType != null)
        {
            var config = Activator.CreateInstance(divisionConfigType);
            if (config != null)
            {
                divisionConfigType.GetField("level")?.SetValue(config, level);
                divisionConfigType.GetField("population")?.SetValue(config, population);
                divisionConfigType.GetField("count")?.SetValue(config, 1000);
                divisionConfigType.GetField("category")?.SetValue(config, 0);
                divisionConfigType.GetField("max_population")?.SetValue(config, population * 2);
                return config;
            }
        }

        throw new InvalidOperationException("Cannot create DivisionConfig instance");
    }
}

// 简单的测试选项监视器实现
public class TestOptionsMonitor : IOptionsMonitor<ServerConfigService.GameDataTable>
{
    public ServerConfigService.GameDataTable CurrentValue => new()
    {
        ConfigFile = new List<ServerConfigService.ConfigUnit>(),
        EnableLocalCache = false
    };

    public ServerConfigService.GameDataTable Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<ServerConfigService.GameDataTable, string?> listener) =>
        new DummyDisposable();
}

// 简单的一次性资源实现
public class DummyDisposable : IDisposable
{
    public void Dispose() { }
}

// 简单的测试日志实现
public class TestLogger : ILogger<ServerConfigService>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}
