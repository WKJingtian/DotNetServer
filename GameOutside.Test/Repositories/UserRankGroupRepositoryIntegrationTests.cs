using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using GameOutside.Repositories;
using GameOutside.Test.Infrastructure;
using Xunit.Abstractions;

namespace GameOutside.Test.Repositories;

public class UserRankGroupRepositoryIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const int TestSeasonNumber = 99999; // 使用大数字避免影响正常业务数据

    private readonly CustomWebApplicationFactory _factory;
    private readonly IServiceScope _scope;
    private readonly IUserRankGroupRepository _repository;
    private readonly TestableServerConfigService _testServerConfigService;
    private readonly IConnectionMultiplexer _redisConnection;
    private readonly ITestOutputHelper _output;

    public UserRankGroupRepositoryIntegrationTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _scope = _factory.Services.CreateScope();

        _repository = _scope.ServiceProvider.GetRequiredService<IUserRankGroupRepository>();
        _testServerConfigService = (TestableServerConfigService)_scope.ServiceProvider.GetRequiredService<ServerConfigService>();
        _redisConnection = _scope.ServiceProvider.GetRequiredKeyedService<IConnectionMultiplexer>("GlobalCache");

        // 清理测试数据
        CleanupRedisData().Wait();
    }

    public void Dispose()
    {
        // 清理测试数据
        CleanupRedisData().Wait();
        _scope?.Dispose();
    }

    private async Task CleanupRedisData()
    {
        try
        {
            var redis = _redisConnection.GetDatabase();
            var server = _redisConnection.GetServer(_redisConnection.GetEndPoints().First());

            // 清理所有测试相关的 Redis 键
            var keys = server.Keys(pattern: $"rank_group:{TestSeasonNumber}:*").Concat(
                server.Keys(pattern: $"rank_group:{TestSeasonNumber + 1}:*")).ToArray();
            if (keys.Length > 0)
            {
                await redis.KeyDeleteAsync(keys);
            }
        }
        catch
        {
            // 忽略清理错误，继续测试
        }
    }

    [Fact]
    public async Task GetGroupIdAsync_FirstCall_ReturnsZero()
    {
        // Arrange
        var seasonNumber = TestSeasonNumber;
        var divisionNumber = 0;

        // Act
        var groupId = await _repository.GetGroupIdAsync(seasonNumber, divisionNumber);

        // Assert
        Assert.Equal(0, groupId);
    }

    [Fact]
    public async Task GetGroupIdAsync_MultipleCalls_SameGroup_IncrementsCounterButSameGroup()
    {
        // Arrange
        var seasonNumber = TestSeasonNumber;
        var divisionNumber = 0;
        _testServerConfigService.SetRankPopulationCap(divisionNumber, 5); // 设置分组最大人数为5

        // Act - 连续获取分组ID，但不超过最大人数
        var groupIds = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            var groupId = await _repository.GetGroupIdAsync(seasonNumber, divisionNumber);
            groupIds.Add(groupId);
        }

        // Assert - 所有调用都应该返回相同的分组ID (0)
        Assert.All(groupIds, id => Assert.Equal(0, id));
        Assert.Equal(5, groupIds.Count);
    }

    [Fact]
    public async Task GetGroupIdAsync_ExceedsGroupCapacity_CreatesNewGroup()
    {
        // Arrange
        var seasonNumber = TestSeasonNumber;
        var divisionNumber = 0;
        _testServerConfigService.SetRankPopulationCap(divisionNumber, 3); // 设置分组最大人数为3

        // Act - 获取超过最大人数的分组ID
        var groupIds = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            var groupId = await _repository.GetGroupIdAsync(seasonNumber, divisionNumber);
            groupIds.Add(groupId);
        }

        // Assert
        Assert.Equal(0, groupIds[0]); // 第1个用户在分组0
        Assert.Equal(0, groupIds[1]); // 第2个用户在分组0
        Assert.Equal(0, groupIds[2]); // 第3个用户在分组0
        Assert.Equal(1, groupIds[3]); // 第4个用户在分组1（新分组）
        Assert.Equal(1, groupIds[4]); // 第5个用户在分组1
    }

    [Fact]
    public async Task GetGroupIdAsync_DifferentSeasons_IndependentGroups()
    {
        // Arrange
        var divisionNumber = 0;
        _testServerConfigService.SetRankPopulationCap(divisionNumber, 2);

        // Act - 在不同赛季获取分组ID
        var season1Group1 = await _repository.GetGroupIdAsync(TestSeasonNumber, divisionNumber);
        var season1Group2 = await _repository.GetGroupIdAsync(TestSeasonNumber, divisionNumber);
        var season1Group3 = await _repository.GetGroupIdAsync(TestSeasonNumber, divisionNumber); // 应该创建新分组

        var season2Group1 = await _repository.GetGroupIdAsync(TestSeasonNumber + 1, divisionNumber);
        var season2Group2 = await _repository.GetGroupIdAsync(TestSeasonNumber + 1, divisionNumber);

        // Assert
        Assert.Equal(0, season1Group1);
        Assert.Equal(0, season1Group2);
        Assert.Equal(1, season1Group3); // 赛季1的新分组

        Assert.Equal(0, season2Group1); // 赛季2从0开始
        Assert.Equal(0, season2Group2);
    }

    [Fact]
    public async Task GetGroupIdAsync_DifferentDivisions_IndependentGroups()
    {
        // Arrange
        var seasonNumber = TestSeasonNumber;
        _testServerConfigService.SetRankPopulationCap(0, 2); // 段位0最大人数2
        _testServerConfigService.SetRankPopulationCap(1, 3); // 段位1最大人数3

        // Act
        var division0Group1 = await _repository.GetGroupIdAsync(seasonNumber, 0);
        var division0Group2 = await _repository.GetGroupIdAsync(seasonNumber, 0);
        var division0Group3 = await _repository.GetGroupIdAsync(seasonNumber, 0); // 应该创建新分组

        var division1Group1 = await _repository.GetGroupIdAsync(seasonNumber, 1);
        var division1Group2 = await _repository.GetGroupIdAsync(seasonNumber, 1);
        var division1Group3 = await _repository.GetGroupIdAsync(seasonNumber, 1);

        // Assert
        Assert.Equal(0, division0Group1);
        Assert.Equal(0, division0Group2);
        Assert.Equal(1, division0Group3); // 段位0的新分组

        Assert.Equal(0, division1Group1); // 段位1从0开始
        Assert.Equal(0, division1Group2);
        Assert.Equal(0, division1Group3); // 段位1还在第一个分组
    }

    [Fact]
    public async Task GetAllGroupIdsAsync_NoGroups_ReturnsEmpty()
    {
        // Arrange
        var seasonNumber = TestSeasonNumber;
        var divisionNumber = 0;

        // Act
        var groupIds = await _repository.GetAllGroupIdsAsync(seasonNumber, divisionNumber);

        // Assert
        Assert.Empty(groupIds);
    }

    [Fact]
    public async Task GetAllGroupIdsAsync_WithGroups_ReturnsAllGroupIds()
    {
        // Arrange
        var seasonNumber = TestSeasonNumber;
        var divisionNumber = 0;
        _testServerConfigService.SetRankPopulationCap(divisionNumber, 2);

        // 先创建一些分组
        await _repository.GetGroupIdAsync(seasonNumber, divisionNumber); // 分组0

        // Act
        var groupIds = await _repository.GetAllGroupIdsAsync(seasonNumber, divisionNumber);

        // Assert
        var groupIdsList = groupIds.ToList();
        Assert.Equal(1, groupIdsList.Count); // 应该有1个分组 (0)
        Assert.Contains(0, groupIdsList);

        // Arrange，再创建一些分组
        await _repository.GetGroupIdAsync(seasonNumber, divisionNumber); // 分组0
        await _repository.GetGroupIdAsync(seasonNumber, divisionNumber); // 分组1
        await _repository.GetGroupIdAsync(seasonNumber, divisionNumber); // 分组1
        await _repository.GetGroupIdAsync(seasonNumber, divisionNumber); // 分组2

        // Act
        groupIds = await _repository.GetAllGroupIdsAsync(seasonNumber, divisionNumber);

        // Assert
        groupIdsList = groupIds.ToList();
        Assert.Equal(3, groupIdsList.Count); // 应该有3个分组 (0, 1, 2)
        Assert.Contains(0, groupIdsList);
        Assert.Contains(1, groupIdsList);
        Assert.Contains(2, groupIdsList);
    }

    [Fact]
    public async Task ClearGroupIdsAsync_RemovesAllGroupsForSeason()
    {
        // Arrange
        var seasonNumber = TestSeasonNumber;
        var divisionNumber = 0;
        _testServerConfigService.SetRankPopulationCap(divisionNumber, 1);

        // 创建一些分组
        await _repository.GetGroupIdAsync(seasonNumber, divisionNumber);
        await _repository.GetGroupIdAsync(seasonNumber, divisionNumber);
        await _repository.GetGroupIdAsync(seasonNumber, divisionNumber);

        // 验证分组存在
        var groupsBeforeClear = await _repository.GetAllGroupIdsAsync(seasonNumber, divisionNumber);
        Assert.NotEmpty(groupsBeforeClear);

        // Act
        await _repository.ClearGroupIdsAsync([seasonNumber]);

        // Assert
        var groupsAfterClear = await _repository.GetAllGroupIdsAsync(seasonNumber, divisionNumber);
        Assert.Empty(groupsAfterClear);
    }

    [Fact]
    public async Task ClearGroupIdsAsync_OnlyAffectsTargetSeason()
    {
        // Arrange
        var divisionNumber = 0;
        _testServerConfigService.SetRankPopulationCap(divisionNumber, 1);

        // 在两个不同赛季创建分组
        await _repository.GetGroupIdAsync(TestSeasonNumber, divisionNumber);
        await _repository.GetGroupIdAsync(TestSeasonNumber, divisionNumber);
        await _repository.GetGroupIdAsync(TestSeasonNumber + 1, divisionNumber);
        await _repository.GetGroupIdAsync(TestSeasonNumber + 1, divisionNumber);

        // Act - 只清除赛季1的分组
        await _repository.ClearGroupIdsAsync([TestSeasonNumber]);

        // Assert
        var season1Groups = await _repository.GetAllGroupIdsAsync(TestSeasonNumber, divisionNumber);
        var season2Groups = await _repository.GetAllGroupIdsAsync(TestSeasonNumber + 1, divisionNumber);

        Assert.Empty(season1Groups); // 赛季1的分组应该被清除
        Assert.NotEmpty(season2Groups); // 赛季2的分组应该保留
    }

    [Fact]
    public async Task GetGroupIdAsync_ConcurrentCalls_ThreadSafe()
    {
        // Arrange
        var seasonNumber = TestSeasonNumber;
        var divisionNumber = 0;
        _testServerConfigService.SetRankPopulationCap(divisionNumber, 5);

        // Act - 并发调用
        var tasks = new List<Task<int>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_repository.GetGroupIdAsync(seasonNumber, divisionNumber).AsTask());
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        var group0Count = results.Count(r => r == 0);
        var group1Count = results.Count(r => r == 1);

        // 由于Lua脚本的原子性，前5个应该在分组0，后5个应该在分组1
        Assert.Equal(5, group0Count);
        Assert.Equal(5, group1Count);
    }

    [Fact]
    public async Task GetGroupIdAsync_LargeScale_HandlesCorrectly()
    {
        // Arrange
        var seasonNumber = TestSeasonNumber;
        var divisionNumber = 0;
        var maxGroupSize = 50;
        var totalUsers = 150; // 应该创建3个分组
        _testServerConfigService.SetRankPopulationCap(divisionNumber, maxGroupSize);

        // Act
        var groupIds = new List<int>();
        for (int i = 0; i < totalUsers; i++)
        {
            var groupId = await _repository.GetGroupIdAsync(seasonNumber, divisionNumber);
            groupIds.Add(groupId);
        }

        // Assert
        var group0Count = groupIds.Count(g => g == 0);
        var group1Count = groupIds.Count(g => g == 1);
        var group2Count = groupIds.Count(g => g == 2);

        Assert.Equal(maxGroupSize, group0Count); // 分组0应该有50个用户
        Assert.Equal(maxGroupSize, group1Count); // 分组1应该有50个用户
        Assert.Equal(maxGroupSize, group2Count); // 分组2应该有50个用户

        // 验证所有分组都被获取到
        var allGroups = await _repository.GetAllGroupIdsAsync(seasonNumber, divisionNumber);
        var groupsList = allGroups.ToList();
        Assert.Equal(3, groupsList.Count);
        Assert.Contains(0, groupsList);
        Assert.Contains(1, groupsList);
        Assert.Contains(2, groupsList);
    }

    [Fact]
    public async Task GetGroupIdAsync_EdgeCase_SingleUserPerGroup()
    {
        // Arrange
        var seasonNumber = TestSeasonNumber;
        var divisionNumber = 0;
        _testServerConfigService.SetRankPopulationCap(divisionNumber, 1); // 每个分组只能有1个用户

        // Act
        var groupIds = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            var groupId = await _repository.GetGroupIdAsync(seasonNumber, divisionNumber);
            groupIds.Add(groupId);
        }

        // Assert
        Assert.Equal(0, groupIds[0]);
        Assert.Equal(1, groupIds[1]);
        Assert.Equal(2, groupIds[2]);
        Assert.Equal(3, groupIds[3]);
        Assert.Equal(4, groupIds[4]);

        // 验证所有分组都存在
        var allGroups = await _repository.GetAllGroupIdsAsync(seasonNumber, divisionNumber);
        var groupsList = allGroups.ToList();
        Assert.Equal(5, groupsList.Count);
        for (int i = 0; i < 5; i++)
        {
            Assert.Contains(i, groupsList);
        }
    }

    [Fact]
    public async Task GetGroupIdAsync_ConcurrencyPerformance_HighLoadWithMeasurement()
    {
        // Arrange
        var seasonNumber = TestSeasonNumber;
        var divisionNumber = 0;
        var maxGroupSize = 100;
        var concurrentUsers = 30000; // 模拟并发用户
        _testServerConfigService.SetRankPopulationCap(divisionNumber, maxGroupSize);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - 高并发调用
        var tasks = new List<Task<int>>();
        var semaphore = new SemaphoreSlim(100, 100); // 限制并发度为100，模拟真实场景

        for (int i = 0; i < concurrentUsers; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await _repository.GetGroupIdAsync(seasonNumber, divisionNumber);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert - 验证性能和正确性

        // 1. 验证总耗时（应该在合理范围内，比如小于5秒）
        Assert.True(stopwatch.ElapsedMilliseconds < 5000,
            $"Performance test failed: took {stopwatch.ElapsedMilliseconds}ms for {concurrentUsers} concurrent calls");

        // 2. 验证分组分布的正确性
        var groupCounts = results.GroupBy(r => r).ToDictionary(g => g.Key, g => g.Count());
        var expectedGroupCount = (int)Math.Ceiling((double)concurrentUsers / maxGroupSize);

        Assert.Equal(expectedGroupCount, groupCounts.Count);

        // 3. 验证每个分组的用户数不超过最大限制（除了最后一个分组可能不满）
        for (int i = 0; i < expectedGroupCount - 1; i++)
        {
            Assert.True(groupCounts.ContainsKey(i), $"Group {i} should exist");
            Assert.Equal(maxGroupSize, groupCounts[i]);
        }

        // 4. 验证最后一个分组的用户数
        var lastGroupId = expectedGroupCount - 1;
        var expectedLastGroupSize = concurrentUsers % maxGroupSize == 0 ? maxGroupSize : concurrentUsers % maxGroupSize;
        Assert.Equal(expectedLastGroupSize, groupCounts[lastGroupId]);

        // 5. 验证没有数据丢失
        Assert.Equal(concurrentUsers, results.Length);

        // 6. 验证分组ID的连续性
        var allGroups = await _repository.GetAllGroupIdsAsync(seasonNumber, divisionNumber);
        var groupsList = allGroups.OrderBy(x => x).ToList();
        for (int i = 0; i < expectedGroupCount; i++)
        {
            Assert.Contains(i, groupsList);
        }

        // 输出性能统计信息（在测试输出中可见）
        var avgTimePerCall = (double)stopwatch.ElapsedMilliseconds / concurrentUsers;
        _output.WriteLine($"Concurrent performance test completed:");
        _output.WriteLine($"  - Total time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  - Average time per call: {avgTimePerCall:F2}ms");
        _output.WriteLine($"  - Throughput: {concurrentUsers * 1000.0 / stopwatch.ElapsedMilliseconds:F2} calls/second");
        _output.WriteLine($"  - Groups created: {expectedGroupCount}");
    }

    [Fact]
    public async Task GetGroupIdAsync_ExtremeConcurrency_StressTest()
    {
        // Arrange
        var seasonNumber = TestSeasonNumber;
        var divisionNumber = 0;
        var maxGroupSize = 50;
        var batchSize = 30000; // 每批次的并发数
        var batchCount = 5;  // 批次数量
        var totalUsers = batchSize * batchCount;
        _testServerConfigService.SetRankPopulationCap(divisionNumber, maxGroupSize);

        var allResults = new List<int>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - 分批进行极限并发测试
        for (int batch = 0; batch < batchCount; batch++)
        {
            var batchTasks = new List<Task<int>>();

            for (int i = 0; i < batchSize; i++)
            {
                batchTasks.Add(_repository.GetGroupIdAsync(seasonNumber, divisionNumber).AsTask());
            }

            var batchResults = await Task.WhenAll(batchTasks);
            allResults.AddRange(batchResults);

            // 短暂延迟以观察批次间的行为
            await Task.Delay(10);
        }

        stopwatch.Stop();

        // Assert

        // 1. 验证没有数据丢失
        Assert.Equal(totalUsers, allResults.Count);

        // 2. 验证分组分布
        var groupCounts = allResults.GroupBy(r => r).ToDictionary(g => g.Key, g => g.Count());
        var expectedGroupCount = (int)Math.Ceiling((double)totalUsers / maxGroupSize);

        // 3. 验证分组数量正确
        Assert.Equal(expectedGroupCount, groupCounts.Count);

        // 4. 验证分组大小限制
        foreach (var kvp in groupCounts)
        {
            Assert.True(kvp.Value <= maxGroupSize,
                $"Group {kvp.Key} has {kvp.Value} users, exceeding max size {maxGroupSize}");
        }

        // 5. 验证极限并发下的性能（允许更长的时间）
        Assert.True(stopwatch.ElapsedMilliseconds < 10000,
            $"Stress test failed: took {stopwatch.ElapsedMilliseconds}ms for {totalUsers} users in {batchCount} batches");

        // 6. 验证数据一致性 - 检查Redis中的实际分组数据
        var allGroups = await _repository.GetAllGroupIdsAsync(seasonNumber, divisionNumber);
        Assert.Equal(expectedGroupCount, allGroups.Count());

        _output.WriteLine($"Stress test completed:");
        _output.WriteLine($"  - Total users: {totalUsers}");
        _output.WriteLine($"  - Batches: {batchCount} x {batchSize}");
        _output.WriteLine($"  - Total time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"  - Groups created: {expectedGroupCount}");
        _output.WriteLine($"  - Data consistency: PASSED");
    }
}
