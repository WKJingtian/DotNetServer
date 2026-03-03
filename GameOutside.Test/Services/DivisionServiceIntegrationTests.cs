using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using GameOutside.Test.Infrastructure;
using GameOutside.Services;
using GameOutside.Models;
using GameOutside.Repositories;
using GameOutside.DBContext;
using Xunit.Abstractions;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Microsoft.Extensions.Logging;

namespace GameOutside.Test.Services;

public class DivisionServiceIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly IServiceScope _scope;
    private readonly DivisionService _divisionService;
    private readonly UserRankService _userRankService;
    private readonly UserEndlessRankService _userEndlessRankService;
    private readonly IUserDivisionRepository _userDivisionRepository;
    private readonly IUserRankRepository _userRankRepository;
    private readonly IUserRankGroupRepository _userRankGroupRepository;
    private readonly ISeasonRefreshedHistoryRepository _seasonRefreshedHistoryRepository;
    private readonly LeaderboardModule _leaderboardModule;
    private readonly BuildingGameDB _dbContext;
    private readonly IConnectionMultiplexer _redisConnection;
    private readonly ITestOutputHelper _output;

    // 测试数据常量
    private const short TestShardId = 1051;
    private const long TestPlayerId1 = 999001L;
    private const long TestPlayerId2 = 999002L;
    private const long TestPlayerId3 = 999003L;
    private const int MockCurrentSeason = 99999; // 模拟当前赛季号
    private const int MockLastSeason = MockCurrentSeason - 1; // 模拟上个赛季号

    // Mock 的赛季信息仓储，返回固定的赛季号
    private class MockSeasonService : SeasonService
    {
        private readonly int _mockSeasonNumber;

        public MockSeasonService(int mockSeasonNumber) : base(new MockSeasonInfoRepository(mockSeasonNumber))
        {
            _mockSeasonNumber = mockSeasonNumber;
        }
    }

    private class MockSeasonInfoRepository : ISeasonInfoRepository
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

    public DivisionServiceIntegrationTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _scope = _factory.Services.CreateScope();

        var serviceProvider = _scope.ServiceProvider;

        // 获取所需的服务
        _userRankService = serviceProvider.GetRequiredService<UserRankService>();
        _userEndlessRankService = serviceProvider.GetRequiredService<UserEndlessRankService>();
        _userDivisionRepository = serviceProvider.GetRequiredService<IUserDivisionRepository>();
        _userRankRepository = serviceProvider.GetRequiredService<IUserRankRepository>();
        _userRankGroupRepository = serviceProvider.GetRequiredService<IUserRankGroupRepository>();
        _seasonRefreshedHistoryRepository = serviceProvider.GetRequiredService<ISeasonRefreshedHistoryRepository>();
        _leaderboardModule = serviceProvider.GetRequiredService<LeaderboardModule>();
        _dbContext = serviceProvider.GetRequiredService<BuildingGameDB>();
        _redisConnection = serviceProvider.GetRequiredKeyedService<IConnectionMultiplexer>("GlobalCache");

        // 创建 DivisionService 实例，使用模拟的 SeasonService
        _divisionService = CreateDivisionServiceWithMockSeason(MockCurrentSeason);

        // 清理测试数据
        CleanupTestData().Wait();
    }

    public void Dispose()
    {
        CleanupTestData().Wait();
        _scope?.Dispose();
    }

    private async Task CleanupTestData()
    {
        try
        {
            // 清理测试用户段位数据
            await _dbContext.UserDivisions
                .Where(ud => ud.PlayerId == TestPlayerId1 ||
                            ud.PlayerId == TestPlayerId2 ||
                            ud.PlayerId == TestPlayerId3)
                .ExecuteDeleteAsync();

            // 清理测试用户排名数据
            await _dbContext.UserRanks
                .Where(ur => ur.SeasonNumber == MockLastSeason ||
                            ur.SeasonNumber == MockCurrentSeason ||
                            ur.PlayerId == TestPlayerId1 ||
                            ur.PlayerId == TestPlayerId2 ||
                            ur.PlayerId == TestPlayerId3)
                .ExecuteDeleteAsync();

            // 清理测试用户无尽模式排名数据
            await _dbContext.UserEndlessRanks
                .Where(uer => uer.SeasonNumber == MockLastSeason ||
                             uer.SeasonNumber == MockCurrentSeason ||
                             uer.PlayerId == TestPlayerId1 ||
                             uer.PlayerId == TestPlayerId2 ||
                             uer.PlayerId == TestPlayerId3)
                .ExecuteDeleteAsync();

            // 清理测试赛季刷新历史记录
            await _dbContext.SeasonRefreshedHistories
                .Where(srh => srh.SeasonNumber == MockLastSeason ||
                             srh.SeasonNumber == MockCurrentSeason)
                .ExecuteDeleteAsync();

            await _dbContext.SaveChangesAsync();

            // 清理排行榜数据
            await _leaderboardModule.ClearLeaderBoard(LeaderboardModule.TowerDefenceModeLeaderBoardId, MockCurrentSeason);
            await _leaderboardModule.ClearLeaderBoard(LeaderboardModule.TrueEndlessModeLeaderBoardId, MockCurrentSeason);
            await _leaderboardModule.ClearLeaderBoard(LeaderboardModule.NormalModeLeaderBoardId, MockCurrentSeason);
            await _leaderboardModule.ClearLeaderBoard(LeaderboardModule.SurvivorModeLeaderBoardId, MockCurrentSeason);
            await _leaderboardModule.ClearLeaderBoard(LeaderboardModule.TowerDefenceModeLeaderBoardId, MockLastSeason);
            await _leaderboardModule.ClearLeaderBoard(LeaderboardModule.TrueEndlessModeLeaderBoardId, MockLastSeason);
            await _leaderboardModule.ClearLeaderBoard(LeaderboardModule.NormalModeLeaderBoardId, MockLastSeason);
            await _leaderboardModule.ClearLeaderBoard(LeaderboardModule.SurvivorModeLeaderBoardId, MockLastSeason);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"清理测试数据时出错: {ex.Message}");
            // 忽略清理错误，继续测试
        }
    }

    [Fact]
    public async Task RefreshDivisionAsync_WithValidLastSeason_ShouldRefreshSuccessfully()
    {
        // Arrange
        await SetupTestDataForRefresh();

        // Act
        await _divisionService.RefreshDivisionAsync();

        // Assert
        await VerifyRefreshResults();
    }

    [Fact]
    public async Task RefreshDivisionAsync_WithFirstSeason_ShouldReturnEarly()
    {
        // Arrange - 设置为第一个赛季 (currentSeason = 0)
        var firstSeasonDivisionService = CreateDivisionServiceWithMockSeason(0);

        // Act
        await firstSeasonDivisionService.RefreshDivisionAsync();

        // Assert
        // 验证没有创建赛季刷新历史记录
        var refreshHistory = await _dbContext.SeasonRefreshedHistories
            .FirstOrDefaultAsync(srh => srh.SeasonNumber == -1); // lastSeason would be -1

        Assert.Null(refreshHistory);
        _output.WriteLine("第一个赛季时，RefreshDivisionAsync 正确地提前返回");
    }

    [Fact]
    public async Task RefreshDivisionAsync_ShouldClearLeaderboards()
    {
        // Arrange
        await SetupTestDataForRefresh();

        // 设置一些上个赛季的排行榜数据 (使用 ClearLeaderBoard 和 UpdateScore 的特定赛季支持)
        // 注意：由于 UpdateScore 不支持 season 参数，我们需要模拟 seasonInfoRepository 返回上个赛季
        var mockSeasonService = new MockSeasonService(MockLastSeason);
        var leaderboardForLastSeason = new LeaderboardModule(_redisConnection, new MockSeasonInfoRepository(MockLastSeason));

        await leaderboardForLastSeason.UpdateScore(TestPlayerId1, 1000, 0, LeaderboardModule.NormalModeLeaderBoardId);
        await leaderboardForLastSeason.UpdateScore(TestPlayerId2, 2000, 0, LeaderboardModule.TowerDefenceModeLeaderBoardId);

        // 验证设置成功
        var initialNormalModeCount = await leaderboardForLastSeason.GetLeaderBoardCardinality(LeaderboardModule.NormalModeLeaderBoardId);
        var initialTowerDefenceCount = await leaderboardForLastSeason.GetLeaderBoardCardinality(LeaderboardModule.TowerDefenceModeLeaderBoardId);

        Assert.True(initialNormalModeCount > 0);
        Assert.True(initialTowerDefenceCount > 0);

        // Act
        await _divisionService.RefreshDivisionAsync();

        // Assert
        // 验证上个赛季的排行榜已被清空
        var normalModeCount = await leaderboardForLastSeason.GetLeaderBoardCardinality(LeaderboardModule.NormalModeLeaderBoardId);
        var towerDefenceCount = await leaderboardForLastSeason.GetLeaderBoardCardinality(LeaderboardModule.TowerDefenceModeLeaderBoardId);

        Assert.Equal(0, normalModeCount);
        Assert.Equal(0, towerDefenceCount);
        _output.WriteLine($"上个赛季({MockLastSeason})的排行榜数据已成功清空");
    }

    [Fact]
    public async Task RefreshDivisionAsync_ShouldUpdateUserDivisionScores()
    {
        // Arrange
        await SetupTestDataForRefresh();

        // 获取刷新前的段位分数
        var userDivisionBefore = await _divisionService.GetUserDivisionAsync(TestShardId, TestPlayerId1, TrackingOptions.NoTracking);
        var originalScore = userDivisionBefore?.DivisionScore ?? 0;

        // Act
        await _divisionService.RefreshDivisionAsync();

        // Assert
        // 验证用户段位分数已更新
        var userDivisionAfter = await _divisionService.GetUserDivisionAsync(TestShardId, TestPlayerId1, TrackingOptions.NoTracking);

        Assert.NotNull(userDivisionAfter);
        Assert.Equal(MockLastSeason, userDivisionAfter.LastSeasonNumber);
        Assert.False(userDivisionAfter.RewardReceived);
        Assert.True(userDivisionAfter.DivisionScore >= originalScore); // 分数应该增加或保持不变

        _output.WriteLine($"用户段位分数已更新: {originalScore} -> {userDivisionAfter.DivisionScore}");
    }

    [Fact]
    public async Task RefreshDivisionAsync_ShouldCreateSeasonRefreshHistory()
    {
        // Arrange
        await SetupTestDataForRefresh();

        // Act
        await _divisionService.RefreshDivisionAsync();

        // Assert
        // 验证赛季刷新历史记录已创建
        var refreshHistory = await _dbContext.SeasonRefreshedHistories
            .FirstOrDefaultAsync(srh => srh.SeasonNumber == MockLastSeason);

        Assert.NotNull(refreshHistory);
        Assert.True(refreshHistory.RefreshedTime <= DateTime.UtcNow);
        Assert.True(refreshHistory.RefreshedTime >= DateTime.UtcNow.AddMinutes(-1)); // 应该在最近1分钟内

        _output.WriteLine($"赛季刷新历史记录已创建: Season {refreshHistory.SeasonNumber} at {refreshHistory.RefreshedTime}");
    }

    [Fact]
    public async Task RefreshDivisionAsync_ShouldClearUserRanks()
    {
        // Arrange
        await SetupTestDataForRefresh();

        // 验证初始状态有排名数据
        var initialRankCount = await _dbContext.UserRanks
            .CountAsync(ur => ur.SeasonNumber == MockLastSeason);
        Assert.True(initialRankCount > 0);

        // Act
        await _divisionService.RefreshDivisionAsync();

        // Assert
        // 验证用户排名数据已清空
        var finalRankCount = await _dbContext.UserRanks
            .CountAsync(ur => ur.SeasonNumber == MockLastSeason);

        Assert.Equal(0, finalRankCount);
        _output.WriteLine($"用户排名数据已清空: {initialRankCount} -> {finalRankCount}");
    }

    [Fact]
    public async Task RefreshDivisionAsync_ShouldHandleMultipleDivisions()
    {
        // Arrange
        await SetupTestDataForMultipleDivisions();

        // Act
        await _divisionService.RefreshDivisionAsync();

        // Assert
        // 验证多个段位的用户都被正确处理
        var division1Users = await _dbContext.UserDivisions
            .Where(ud => ud.LastSeasonNumber == MockLastSeason &&
                        (ud.PlayerId == TestPlayerId1 || ud.PlayerId == TestPlayerId2))
            .ToListAsync();

        Assert.All(division1Users, ud =>
        {
            Assert.Equal(MockLastSeason, ud.LastSeasonNumber);
            Assert.False(ud.RewardReceived);
        });

        _output.WriteLine($"多段位用户处理完成，共处理 {division1Users.Count} 个用户");
    }

    private async Task SetupTestDataForRefresh()
    {
        // 创建测试用户段位数据（上次刷新是上个赛季的上个赛季）
        var userDivision1 = CreateTestUserDivision(TestPlayerId1, 1500, 1500, MockLastSeason - 1);
        var userDivision2 = CreateTestUserDivision(TestPlayerId2, 1200, 1200, MockLastSeason - 1);

        _dbContext.UserDivisions.Add(userDivision1);
        _dbContext.UserDivisions.Add(userDivision2);

        // 创建测试用户排名数据 (上个赛季)
        var userRank1 = CreateTestUserRank(TestPlayerId1, MockLastSeason, 1, 1500, 0, true);
        var userRank2 = CreateTestUserRank(TestPlayerId2, MockLastSeason, 1, 1200, 0, false);

        _userRankRepository.AddUserRank(userRank1);
        _userRankRepository.AddUserRank(userRank2);

        // 创建测试用户无尽模式排名数据
        var userEndlessRank1 = CreateTestUserEndlessRank(TestPlayerId1, MockLastSeason);
        var userEndlessRank2 = CreateTestUserEndlessRank(TestPlayerId2, MockLastSeason);

        _dbContext.UserEndlessRanks.Add(userEndlessRank1);
        _dbContext.UserEndlessRanks.Add(userEndlessRank2);

        await _dbContext.SaveChangesAsync();

        // 设置 Redis 中的分组 ID 数据
        var database = _redisConnection.GetDatabase();
        await database.StringSetAsync($"rank_group:{MockLastSeason}:1:group_id", 0);
    }

    private async Task SetupTestDataForMultipleDivisions()
    {
        // 创建不同段位的用户数据
        var userDivision1 = CreateTestUserDivision(TestPlayerId1, 1500, 1500, MockLastSeason - 1);
        var userDivision2 = CreateTestUserDivision(TestPlayerId2, 2500, 2500, MockLastSeason - 1);
        var userDivision3 = CreateTestUserDivision(TestPlayerId3, 1800, 1800, MockLastSeason - 1);

        _dbContext.UserDivisions.Add(userDivision1);
        _dbContext.UserDivisions.Add(userDivision2);
        _dbContext.UserDivisions.Add(userDivision3);

        // 创建对应的排名数据
        var userRank1 = CreateTestUserRank(TestPlayerId1, MockLastSeason, 1, 1500, 0, true);
        var userRank2 = CreateTestUserRank(TestPlayerId2, MockLastSeason, 2, 2500, 0, true);
        var userRank3 = CreateTestUserRank(TestPlayerId3, MockLastSeason, 1, 1800, 0, false);

        _userRankRepository.AddUserRank(userRank1);
        _userRankRepository.AddUserRank(userRank2);
        _userRankRepository.AddUserRank(userRank3);

        await _dbContext.SaveChangesAsync();

        // 设置 Redis 中的分组 ID 数据
        var database = _redisConnection.GetDatabase();
        await database.StringSetAsync($"rank_group:{MockLastSeason}:1:group_id", 0);
        await database.StringSetAsync($"rank_group:{MockLastSeason}:2:group_id", 0);
    }

    private async Task VerifyRefreshResults()
    {
        // 验证赛季刷新历史记录
        var refreshHistory = await _dbContext.SeasonRefreshedHistories
            .FirstOrDefaultAsync(srh => srh.SeasonNumber == MockLastSeason);
        Assert.NotNull(refreshHistory);

        // 验证用户排名数据已清空
        var userRankCount = await _dbContext.UserRanks
            .CountAsync(ur => ur.SeasonNumber == MockLastSeason);
        Assert.Equal(0, userRankCount);

        // 验证用户无尽模式排名数据已清空
        var userEndlessRankCount = await _dbContext.UserEndlessRanks
            .CountAsync(uer => uer.SeasonNumber == MockLastSeason);
        Assert.Equal(0, userEndlessRankCount);

        // 验证用户段位数据已更新
        var userDivisions = await _dbContext.UserDivisions
            .Where(ud => ud.LastSeasonNumber == MockLastSeason)
            .ToListAsync();

        Assert.All(userDivisions, ud =>
        {
            Assert.Equal(MockLastSeason, ud.LastSeasonNumber);
            Assert.False(ud.RewardReceived);
        });

        _output.WriteLine("RefreshDivisionAsync 执行结果验证通过");
    }

    private DivisionService CreateDivisionServiceWithMockSeason(int seasonNumber)
    {
        var mockSeasonService = new MockSeasonService(seasonNumber);
        return new DivisionService(
            _scope.ServiceProvider.GetRequiredService<ServerConfigService>(),
            _leaderboardModule,
            _dbContext,
            _userRankService,
            _userEndlessRankService,
            mockSeasonService,
            _userRankGroupRepository,
            _seasonRefreshedHistoryRepository,
            _userDivisionRepository,
            _scope.ServiceProvider.GetRequiredService<ILogger<DivisionService>>());
    }

    private UserDivision CreateTestUserDivision(long playerId, int divisionScore, int maxDivisionScore, int lastSeasonNumber)
    {
        return new UserDivision
        {
            ShardId = TestShardId,
            PlayerId = playerId,
            DivisionScore = divisionScore,
            MaxDivisionScore = maxDivisionScore,
            LastSeasonNumber = lastSeasonNumber,
            RewardReceived = true,
            LastDivisionScore = divisionScore - 100,
            LastDivisionRank = 5,
            LastWorldRank = -1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private UserRank CreateTestUserRank(long playerId, int seasonNumber, int division, long highestScore, long groupId, bool win)
    {
        return new UserRank
        {
            ShardId = TestShardId,
            PlayerId = playerId,
            SeasonNumber = seasonNumber,
            Division = division,
            HighestScore = highestScore,
            GroupId = groupId,
            Win = win,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private UserEndlessRank CreateTestUserEndlessRank(long playerId, int seasonNumber)
    {
        return new UserEndlessRank
        {
            ShardId = TestShardId,
            PlayerId = playerId,
            SeasonNumber = seasonNumber,
            SurvivorScore = 1000,
            SurvivorTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            TowerDefenceScore = 2000,
            TowerDefenceTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            TrueEndlessScore = 3000,
            TrueEndlessTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }
}
