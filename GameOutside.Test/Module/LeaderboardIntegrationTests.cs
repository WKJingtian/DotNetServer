using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using GameOutside.Test.Infrastructure;
using GameOutside.Repositories;
using Xunit.Abstractions;

namespace GameOutside.Test.Module;

// 模拟的赛季信息仓储，返回固定的赛季号
public class MockSeasonInfoRepository : ISeasonInfoRepository
{
    private readonly int _seasonNumber;

    public MockSeasonInfoRepository(int seasonNumber)
    {
        _seasonNumber = seasonNumber;
    }

    public ValueTask<int> GetCurrentSeasonNumberAsync()
    {
        return ValueTask.FromResult(_seasonNumber);
    }
}

// 该用例使用模拟赛季号，避免操作当前赛季的排行榜数据
public class LeaderboardIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly IServiceScope _scope;
    private readonly LeaderboardModule _leaderboardModule;
    private readonly ISeasonInfoRepository _seasonInfoRepository;
    private readonly IConnectionMultiplexer _redisConnection;
    private readonly ITestOutputHelper _output;

    // 测试数据常量
    private const long TestPlayerId1 = 999001L;
    private const long TestPlayerId2 = 999002L;
    private const long TestPlayerId3 = 999003L;
    private const string TestLeaderboardId = "test_leaderboard";
    private const int MockSeasonNumber = 99999; // 模拟赛季号，避免操作真实赛季数据

    public LeaderboardIntegrationTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _scope = _factory.Services.CreateScope();

        _seasonInfoRepository = new MockSeasonInfoRepository(MockSeasonNumber);

        var redisConnection = _scope.ServiceProvider.GetRequiredKeyedService<IConnectionMultiplexer>("GlobalCache");
        _leaderboardModule = new LeaderboardModule(redisConnection, _seasonInfoRepository);
        _redisConnection = redisConnection;

        // 清理测试数据
        CleanupRedisData().Wait();

        // 验证使用的是模拟赛季号
        _output.WriteLine($"测试使用模拟赛季号: {MockSeasonNumber}");
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

            // 清理模拟赛季的排行榜数据
            await _leaderboardModule.ClearLeaderBoard(TestLeaderboardId, MockSeasonNumber);
            await _leaderboardModule.ClearLeaderBoard(LeaderboardModule.TowerDefenceModeLeaderBoardId, MockSeasonNumber);
            await _leaderboardModule.ClearLeaderBoard(LeaderboardModule.TrueEndlessModeLeaderBoardId, MockSeasonNumber);
            await _leaderboardModule.ClearLeaderBoard(LeaderboardModule.NormalModeLeaderBoardId, MockSeasonNumber);
            await _leaderboardModule.ClearLeaderBoard(LeaderboardModule.SurvivorModeLeaderBoardId, MockSeasonNumber);
        }
        catch
        {
            // 忽略清理错误，继续测试
        }
    }

    [Fact]
    public async Task UpdateScore_NewPlayer_AddsToLeaderboard()
    {
        // Arrange
        var playerId = TestPlayerId1;
        var score = 1000L;
        var oldScore = 0L;

        // Act
        await _leaderboardModule.UpdateScore(playerId, score, oldScore, TestLeaderboardId);

        // Assert
        var rank = await _leaderboardModule.GetPlayerRank(playerId, TestLeaderboardId);
        Assert.Equal(1, rank); // 第一名，排名从1开始

        var cardinality = await _leaderboardModule.GetLeaderBoardCardinality(TestLeaderboardId);
        Assert.Equal(1, cardinality);
    }

    [Fact]
    public async Task UpdateScore_ExistingPlayer_UpdatesScore()
    {
        // Arrange
        var playerId = TestPlayerId1;
        var initialScore = 1000L;
        var newScore = 1500L;

        // 先添加初始分数
        await _leaderboardModule.UpdateScore(playerId, initialScore, 0L, TestLeaderboardId);

        // Act - 更新分数
        await _leaderboardModule.UpdateScore(playerId, newScore, initialScore, TestLeaderboardId);

        // Assert
        var rank = await _leaderboardModule.GetPlayerRank(playerId, TestLeaderboardId);
        Assert.Equal(1, rank);

        var cardinality = await _leaderboardModule.GetLeaderBoardCardinality(TestLeaderboardId);
        Assert.Equal(1, cardinality); // 仍然只有一个玩家

        var topPlayers = await _leaderboardModule.GetTopPlayers(0, 1, TestLeaderboardId);
        Assert.Single(topPlayers);
        Assert.Equal(playerId, topPlayers[0].PlayerId);
        Assert.Equal(newScore, topPlayers[0].Score);
    }

    [Fact]
    public async Task GetTopPlayers_MultiplePlayersCorrectOrder()
    {
        // Arrange
        var scores = new Dictionary<long, long>
        {
            { TestPlayerId1, 1000L },
            { TestPlayerId2, 1500L },
            { TestPlayerId3, 1200L }
        };

        // 添加玩家分数
        foreach (var (playerId, score) in scores)
        {
            await _leaderboardModule.UpdateScore(playerId, score, 0L, TestLeaderboardId);
        }

        // Act
        var topPlayers = await _leaderboardModule.GetTopPlayers(0, 3, TestLeaderboardId);

        // Assert
        Assert.Equal(3, topPlayers.Length);

        // 验证排序：分数高的在前
        Assert.Equal(TestPlayerId2, topPlayers[0].PlayerId); // 1500分，第1名
        Assert.Equal(1500L, topPlayers[0].Score);

        Assert.Equal(TestPlayerId3, topPlayers[1].PlayerId); // 1200分，第2名
        Assert.Equal(1200L, topPlayers[1].Score);

        Assert.Equal(TestPlayerId1, topPlayers[2].PlayerId); // 1000分，第3名
        Assert.Equal(1000L, topPlayers[2].Score);
    }

    [Fact]
    public async Task GetPlayerRank_PlayerNotOnLeaderboard_ReturnsMinusOne()
    {
        // Act
        var rank = await _leaderboardModule.GetPlayerRank(888888L, TestLeaderboardId);

        // Assert
        Assert.Equal(-1, rank);
    }

    [Fact]
    public async Task GetPlayerRankPercentage_ValidScore_ReturnsCorrectPercentage()
    {
        // Arrange - 添加一些玩家分数来建立分布
        var scores = new List<long> { 1000L, 2000L, 3000L, 4000L, 5000L };
        for (int i = 0; i < scores.Count; i++)
        {
            await _leaderboardModule.UpdateScore(TestPlayerId1 + i, scores[i], 0L, TestLeaderboardId);
        }

        // Act - 查询中位数分数的百分比
        var percentage = await _leaderboardModule.GetPlayerRankPercentage(3000L, TestLeaderboardId);

        // Assert - 百分比应该在合理范围内（0到1之间）
        Assert.True(percentage >= 0.0f && percentage <= 1.0f);
        _output.WriteLine($"Score 3000 的百分比: {percentage}");
    }

    [Fact]
    public async Task GetLeaderBoardCardinality_EmptyLeaderboard_ReturnsZero()
    {
        // Act
        var cardinality = await _leaderboardModule.GetLeaderBoardCardinality(TestLeaderboardId);

        // Assert
        Assert.Equal(0, cardinality);
    }

    [Fact]
    public async Task GetLeaderBoardCardinality_WithPlayers_ReturnsCorrectCount()
    {
        // Arrange
        var playerCount = 5;
        for (int i = 0; i < playerCount; i++)
        {
            await _leaderboardModule.UpdateScore(TestPlayerId1 + i, 1000L + i * 100, 0L, TestLeaderboardId);
        }

        // Act
        var cardinality = await _leaderboardModule.GetLeaderBoardCardinality(TestLeaderboardId);

        // Assert
        Assert.Equal(playerCount, cardinality);
    }

    [Fact]
    public async Task RemovePlayerFromLeaderBoard_ExistingPlayer_RemovesSuccessfully()
    {
        // Arrange
        await _leaderboardModule.UpdateScore(TestPlayerId1, 1000L, 0L, TestLeaderboardId);
        await _leaderboardModule.UpdateScore(TestPlayerId2, 1500L, 0L, TestLeaderboardId);

        var initialCardinality = await _leaderboardModule.GetLeaderBoardCardinality(TestLeaderboardId);
        Assert.Equal(2, initialCardinality);

        // Act
        await _leaderboardModule.RemovePlayerFromLeaderBoard(TestPlayerId1, TestLeaderboardId);

        // Assert
        var rank = await _leaderboardModule.GetPlayerRank(TestPlayerId1, TestLeaderboardId);
        Assert.Equal(-1, rank); // 玩家已不在排行榜上

        var cardinality = await _leaderboardModule.GetLeaderBoardCardinality(TestLeaderboardId);
        Assert.Equal(1, cardinality); // 只剩一个玩家

        var topPlayers = await _leaderboardModule.GetTopPlayers(0, 2, TestLeaderboardId);
        Assert.Single(topPlayers);
        Assert.Equal(TestPlayerId2, topPlayers[0].PlayerId);
    }

    [Fact]
    public async Task ClearLeaderBoard_WithData_ClearsSuccessfully()
    {
        // Arrange
        await _leaderboardModule.UpdateScore(TestPlayerId1, 1000L, 0L, TestLeaderboardId);
        await _leaderboardModule.UpdateScore(TestPlayerId2, 1500L, 0L, TestLeaderboardId);

        var initialCardinality = await _leaderboardModule.GetLeaderBoardCardinality(TestLeaderboardId);
        Assert.Equal(2, initialCardinality);

        // Act
        await _leaderboardModule.ClearLeaderBoard(TestLeaderboardId);

        // Assert
        var cardinality = await _leaderboardModule.GetLeaderBoardCardinality(TestLeaderboardId);
        Assert.Equal(0, cardinality);

        var topPlayers = await _leaderboardModule.GetTopPlayers(0, 10, TestLeaderboardId);
        Assert.Empty(topPlayers);
    }

    [Fact]
    public async Task GetTopPlayers_WithRangeParameters_ReturnsCorrectSlice()
    {
        // Arrange - 添加10个玩家
        for (int i = 0; i < 10; i++)
        {
            await _leaderboardModule.UpdateScore(TestPlayerId1 + i, 1000L + i * 100L, 0L, TestLeaderboardId);
        }

        // Act - 获取排名2-4的玩家（索引1-3）
        var topPlayers = await _leaderboardModule.GetTopPlayers(1, 4, TestLeaderboardId);

        // Assert
        Assert.Equal(3, topPlayers.Length);

        // 验证返回的是正确的排名切片（分数从高到低排序）
        Assert.True(topPlayers[0].Score > topPlayers[1].Score);
        Assert.True(topPlayers[1].Score > topPlayers[2].Score);

        _output.WriteLine($"排名2-4的玩家分数: {string.Join(", ", topPlayers.Select(p => p.Score))}");
    }

    [Fact]
    public async Task Integration_CompleteWorkflow_WorksCorrectly()
    {
        // Arrange - 模拟完整的排行榜操作流程
        var initialScores = new Dictionary<long, long>
        {
            { TestPlayerId1, 1000L },
            { TestPlayerId2, 1500L },
            { TestPlayerId3, 1200L }
        };

        // Act1 - 添加初始分数
        foreach (var (playerId, score) in initialScores)
        {
            await _leaderboardModule.UpdateScore(playerId, score, 0L, TestLeaderboardId);
        }

        // 验证初始状态
        var initialCardinality = await _leaderboardModule.GetLeaderBoardCardinality(TestLeaderboardId);
        Assert.Equal(3, initialCardinality);

        var initialTop = await _leaderboardModule.GetTopPlayers(0, 3, TestLeaderboardId);
        Assert.Equal(TestPlayerId2, initialTop[0].PlayerId); // 最高分

        // Act2 - Player1 更新到更高分数
        await _leaderboardModule.UpdateScore(TestPlayerId1, 2000L, 1000L, TestLeaderboardId);

        // 验证排名变化
        var newRank1 = await _leaderboardModule.GetPlayerRank(TestPlayerId1, TestLeaderboardId);
        var newRank2 = await _leaderboardModule.GetPlayerRank(TestPlayerId2, TestLeaderboardId);

        Assert.Equal(1, newRank1); // Player1 现在是第1名
        Assert.Equal(2, newRank2); // Player2 降到第2名

        // Act3 - 移除一个玩家
        await _leaderboardModule.RemovePlayerFromLeaderBoard(TestPlayerId3, TestLeaderboardId);

        // 验证最终状态
        var finalCardinality = await _leaderboardModule.GetLeaderBoardCardinality(TestLeaderboardId);
        Assert.Equal(2, finalCardinality);

        var finalTop = await _leaderboardModule.GetTopPlayers(0, 2, TestLeaderboardId);
        Assert.Equal(2, finalTop.Length);
        Assert.Equal(TestPlayerId1, finalTop[0].PlayerId); // Player1 仍然第1
        Assert.Equal(TestPlayerId2, finalTop[1].PlayerId); // Player2 第2

        _output.WriteLine($"集成测试完成: 初始3个玩家，Player1逆袭第1名，移除Player3后剩余{finalCardinality}个玩家");
    }

    [Theory]
    [InlineData("leader_normal")]
    [InlineData("leader_survivor")]
    [InlineData("leader_tower_defence")]
    [InlineData("leader_true_endless")]
    public async Task PredefinedLeaderboards_BasicOperations_WorkCorrectly(string leaderboardId)
    {
        // Arrange
        var playerId = TestPlayerId1;
        var score = 5000L;

        // Act
        await _leaderboardModule.UpdateScore(playerId, score, 0L, leaderboardId);

        // Assert
        var rank = await _leaderboardModule.GetPlayerRank(playerId, leaderboardId);
        Assert.Equal(1, rank);

        var cardinality = await _leaderboardModule.GetLeaderBoardCardinality(leaderboardId);
        Assert.Equal(1, cardinality);

        var topPlayers = await _leaderboardModule.GetTopPlayers(0, 1, leaderboardId);
        Assert.Single(topPlayers);
        Assert.Equal(playerId, topPlayers[0].PlayerId);
        Assert.Equal(score, topPlayers[0].Score);

        _output.WriteLine($"预定义排行榜 {leaderboardId} 测试完成");
    }

    [Fact]
    public async Task UpdateScore_InvalidScore_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _leaderboardModule.UpdateScore(TestPlayerId1, 0L, 0L, TestLeaderboardId));

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _leaderboardModule.UpdateScore(TestPlayerId1, -100L, 0L, TestLeaderboardId));
    }

    [Fact]
    public async Task GetTopPlayers_InvalidRange_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _leaderboardModule.GetTopPlayers(-1, 5, TestLeaderboardId));

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _leaderboardModule.GetTopPlayers(5, 3, TestLeaderboardId));
    }

    [Fact]
    public async Task GetPlayerRankPercentage_InvalidScore_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _leaderboardModule.GetPlayerRankPercentage(0L, TestLeaderboardId));

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _leaderboardModule.GetPlayerRankPercentage(-100L, TestLeaderboardId));
    }
}