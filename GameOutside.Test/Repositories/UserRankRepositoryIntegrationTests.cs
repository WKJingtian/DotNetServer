using Microsoft.Extensions.DependencyInjection;
using GameOutside.Repositories;
using GameOutside.Test.Infrastructure;
using GameOutside.Models;
using Xunit.Abstractions;
using GameOutside.DBContext;
using Microsoft.EntityFrameworkCore;

namespace GameOutside.Test.Repositories;

public class UserRankRepositoryIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly IServiceScope _scope;
    private readonly IUserRankRepository _repository;
    private readonly ISeasonInfoRepository _seasonInfoRepository;
    private readonly BuildingGameDB _dbContext;
    private readonly ITestOutputHelper _output;

    // 测试数据常量
    private const short TestShardId = 1051;
    private const long TestPlayerId1 = 999001L;
    private const long TestPlayerId2 = 999002L;
    private const long TestPlayerId3 = 999003L;
    private const int TestSeasonNumber = 999;

    public UserRankRepositoryIntegrationTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _scope = _factory.Services.CreateScope();

        _repository = _scope.ServiceProvider.GetRequiredService<IUserRankRepository>();
        _seasonInfoRepository = _scope.ServiceProvider.GetRequiredService<ISeasonInfoRepository>();
        _dbContext = _scope.ServiceProvider.GetRequiredService<BuildingGameDB>();

        // 清理测试数据
        CleanupTestData().Wait();
    }

    public void Dispose()
    {
        // 清理测试数据
        CleanupTestData().Wait();
        _scope?.Dispose();
    }

    private async Task CleanupTestData()
    {
        try
        {
            // 清理测试用户排名数据
            await _dbContext.UserRanks
                .Where(ur => ur.SeasonNumber == TestSeasonNumber ||
                            ur.PlayerId == TestPlayerId1 ||
                            ur.PlayerId == TestPlayerId2 ||
                            ur.PlayerId == TestPlayerId3)
                .ExecuteDeleteAsync();

            await _dbContext.SaveChangesAsync();
        }
        catch
        {
            // 忽略清理错误，继续测试
        }
    }

    [Fact]
    public async Task GetUserRankAsync_WithoutSeasonNumber_ReturnsCurrentSeasonUserRank()
    {
        // Arrange
        var currentSeason = await _seasonInfoRepository.GetCurrentSeasonNumberAsync();
        var userRank = CreateTestUserRank(TestPlayerId1, currentSeason, 0, 1000, true);
        _repository.AddUserRank(userRank);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetUserRankAsync(TestShardId, TestPlayerId1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TestShardId, result.ShardId);
        Assert.Equal(TestPlayerId1, result.PlayerId);
        Assert.Equal(currentSeason, result.SeasonNumber);
        Assert.Equal(1000, result.HighestScore);
    }

    [Fact]
    public async Task GetUserRankAsync_WithSpecificSeasonNumber_ReturnsCorrectSeasonUserRank()
    {
        // Arrange
        var userRank = CreateTestUserRank(TestPlayerId1, TestSeasonNumber, 0, 1500, false);
        _repository.AddUserRank(userRank);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetUserRankAsync(TestShardId, TestPlayerId1, TestSeasonNumber);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TestShardId, result.ShardId);
        Assert.Equal(TestPlayerId1, result.PlayerId);
        Assert.Equal(TestSeasonNumber, result.SeasonNumber);
        Assert.Equal(1500, result.HighestScore);
        Assert.False(result.Win);
    }

    [Fact]
    public async Task GetUserRankAsync_UserNotExists_ReturnsNull()
    {
        // Act
        var result = await _repository.GetUserRankAsync(TestShardId, 888888L, TestSeasonNumber);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveUserRankAsync_ExistingUser_ReturnsTrue()
    {
        // Arrange
        var userRank = CreateTestUserRank(TestPlayerId1, TestSeasonNumber, 1, 2000, true);
        _repository.AddUserRank(userRank);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.RemoveUserRankAsync(TestShardId, TestPlayerId1, TestSeasonNumber);
        await _dbContext.SaveChangesAsync();

        // Assert
        Assert.True(result);

        // Verify removal
        var removedUser = await _repository.GetUserRankAsync(TestShardId, TestPlayerId1, TestSeasonNumber);
        Assert.Null(removedUser);
    }

    [Fact]
    public async Task RemoveUserRankAsync_NonExistingUser_ReturnsFalse()
    {
        // Act
        var result = await _repository.RemoveUserRankAsync(TestShardId, 888888L, TestSeasonNumber);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetUserDivisionRankAsync_ReturnsCorrectRank()
    {
        // Arrange
        var division = 1;
        var groupId = 0L;
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 创建多个用户，按分数排序
        var userRank1 = CreateTestUserRank(TestPlayerId1, TestSeasonNumber, division, 1000, true, groupId, timestamp + 1);
        var userRank2 = CreateTestUserRank(TestPlayerId2, TestSeasonNumber, division, 1200, true, groupId, timestamp + 2); // 更高分数
        var userRank3 = CreateTestUserRank(TestPlayerId3, TestSeasonNumber, division, 1100, false, groupId, timestamp + 3); // 中等分数

        _repository.AddUserRank(userRank1);
        _repository.AddUserRank(userRank2);
        _repository.AddUserRank(userRank3);
        await _dbContext.SaveChangesAsync();

        // Act
        var rank1 = await _repository.GetUserDivisionRankAsync(TestShardId, TestPlayerId1, TestSeasonNumber); // 应该是第3名 (排名从0开始)
        var rank2 = await _repository.GetUserDivisionRankAsync(TestShardId, TestPlayerId2, TestSeasonNumber); // 应该是第1名
        var rank3 = await _repository.GetUserDivisionRankAsync(TestShardId, TestPlayerId3, TestSeasonNumber); // 应该是第2名

        // Assert
        Assert.Equal(2, rank1); // TestPlayerId1 分数最低，排名第3 (index=2)
        Assert.Equal(0, rank2); // TestPlayerId2 分数最高，排名第1 (index=0)
        Assert.Equal(1, rank3); // TestPlayerId3 分数中等，排名第2 (index=1)
    }

    [Fact]
    public async Task GetUserDivisionRankAsync_UserNotExists_ReturnsMinusOne()
    {
        // Act
        var rank = await _repository.GetUserDivisionRankAsync(TestShardId, 888888L);

        // Assert
        Assert.Equal(-1, rank);
    }

    [Fact]
    public async Task AddUserRank_AddsSuccessfully()
    {
        // Arrange
        var userRank = CreateTestUserRank(TestPlayerId1, TestSeasonNumber, 0, 500, true);

        // Act
        _repository.AddUserRank(userRank);
        await _dbContext.SaveChangesAsync();

        // Assert
        var result = await _repository.GetUserRankAsync(TestShardId, TestPlayerId1, TestSeasonNumber);
        Assert.NotNull(result);
        Assert.Equal(500, result.HighestScore);
        Assert.True(result.Win);
    }

    [Fact]
    public async Task ClearUserRanksAsync_NonCurrentSeason_ClearsSuccessfully()
    {
        // Arrange
        var oldSeasonNumber = TestSeasonNumber - 1;
        var userRank1 = CreateTestUserRank(TestPlayerId1, oldSeasonNumber, 0, 1000, true);
        var userRank2 = CreateTestUserRank(TestPlayerId2, oldSeasonNumber, 1, 1200, false);

        _repository.AddUserRank(userRank1);
        _repository.AddUserRank(userRank2);
        await _dbContext.SaveChangesAsync();

        // Verify data exists before clearing
        var beforeClear1 = await _repository.GetUserRankAsync(TestShardId, TestPlayerId1, oldSeasonNumber);
        var beforeClear2 = await _repository.GetUserRankAsync(TestShardId, TestPlayerId2, oldSeasonNumber);
        Assert.NotNull(beforeClear1);
        Assert.NotNull(beforeClear2);
        _dbContext.ChangeTracker.Clear();

        // Act
        await _repository.ClearUserRanksAsync([oldSeasonNumber]);

        // Assert
        var afterClear1 = await _repository.GetUserRankAsync(TestShardId, TestPlayerId1, oldSeasonNumber);
        var afterClear2 = await _repository.GetUserRankAsync(TestShardId, TestPlayerId2, oldSeasonNumber);
        Assert.Null(afterClear1);
        Assert.Null(afterClear2);
    }

    [Fact]
    public async Task ClearUserRanksAsync_CurrentSeason_ThrowsException()
    {
        // Arrange
        var currentSeason = await _seasonInfoRepository.GetCurrentSeasonNumberAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _repository.ClearUserRanksAsync([currentSeason]).AsTask());
    }

    [Fact]
    public async Task UpdateUserScore_MultipleUploads_RankUpdatesCorrectly()
    {
        // Arrange
        var division = 0;
        var groupId = 0L;
        var baseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 创建三个用户的初始排名
        var userRank1 = CreateTestUserRank(TestPlayerId1, TestSeasonNumber, division, 1000, true, groupId, baseTimestamp + 1);
        var userRank2 = CreateTestUserRank(TestPlayerId2, TestSeasonNumber, division, 1200, true, groupId, baseTimestamp + 2);
        var userRank3 = CreateTestUserRank(TestPlayerId3, TestSeasonNumber, division, 1100, false, groupId, baseTimestamp + 3);

        _repository.AddUserRank(userRank1);
        _repository.AddUserRank(userRank2);
        _repository.AddUserRank(userRank3);
        await _dbContext.SaveChangesAsync();

        // 验证初始排名：Player2(1200) > Player3(1100) > Player1(1000)
        var initialRank1 = await _repository.GetUserDivisionRankAsync(TestShardId, TestPlayerId1, TestSeasonNumber);
        var initialRank2 = await _repository.GetUserDivisionRankAsync(TestShardId, TestPlayerId2, TestSeasonNumber);
        var initialRank3 = await _repository.GetUserDivisionRankAsync(TestShardId, TestPlayerId3, TestSeasonNumber);

        Assert.Equal(2, initialRank1); // Player1 排名第3
        Assert.Equal(0, initialRank2); // Player2 排名第1
        Assert.Equal(1, initialRank3); // Player3 排名第2

        // Act1 - Player1 上传更低分数，模拟更新但分数不变的情况
        var existingUser1 = await _repository.GetUserRankAsync(TestShardId, TestPlayerId1, TestSeasonNumber);
        Assert.NotNull(existingUser1);

        // 使用更新函数处理分数更新逻辑
        var newLowerScore = 900L;
        var scoreUpdated1 = UpdateUserScore(existingUser1, newLowerScore, baseTimestamp + 10, false);
        await _dbContext.SaveChangesAsync();

        var afterLowerScoreRank1 = await _repository.GetUserDivisionRankAsync(TestShardId, TestPlayerId1, TestSeasonNumber);
        var afterLowerScoreUser1 = await _repository.GetUserRankAsync(TestShardId, TestPlayerId1, TestSeasonNumber);

        Assert.False(scoreUpdated1); // 分数没有更新
        Assert.Equal(2, afterLowerScoreRank1); // 排名不变
        Assert.NotNull(afterLowerScoreUser1);
        Assert.Equal(1000, afterLowerScoreUser1.HighestScore); // 最高分数保持不变
        Assert.False(afterLowerScoreUser1.Win); // Win状态已更新

        // Act2 - Player1 上传更高分数，超越所有人
        var newHigherScore = 1500L;
        var scoreUpdated2 = UpdateUserScore(existingUser1, newHigherScore, baseTimestamp + 20, true);
        await _dbContext.SaveChangesAsync();

        // 验证Player1成为第一名
        var afterHigherScoreRank1 = await _repository.GetUserDivisionRankAsync(TestShardId, TestPlayerId1, TestSeasonNumber);
        var afterHigherScoreRank2 = await _repository.GetUserDivisionRankAsync(TestShardId, TestPlayerId2, TestSeasonNumber);
        var afterHigherScoreRank3 = await _repository.GetUserDivisionRankAsync(TestShardId, TestPlayerId3, TestSeasonNumber);
        var updatedUser1 = await _repository.GetUserRankAsync(TestShardId, TestPlayerId1, TestSeasonNumber);

        Assert.True(scoreUpdated2); // 分数已更新
        Assert.Equal(0, afterHigherScoreRank1); // Player1 现在排名第1
        Assert.Equal(1, afterHigherScoreRank2); // Player2 降到第2
        Assert.Equal(2, afterHigherScoreRank3); // Player3 降到第3
        Assert.NotNull(updatedUser1);
        Assert.Equal(1500, updatedUser1.HighestScore); // 最高分数已更新
        Assert.True(updatedUser1.Win); // Win状态已更新

        // Act3 - Player3 也上传更高分数，但仍低于Player1
        var existingUser3 = await _repository.GetUserRankAsync(TestShardId, TestPlayerId3, TestSeasonNumber);
        Assert.NotNull(existingUser3);

        var player3NewScore = 1300L;
        var scoreUpdated3 = UpdateUserScore(existingUser3, player3NewScore, baseTimestamp + 30, true);
        await _dbContext.SaveChangesAsync();

        // 验证最终排名：Player1(1500) > Player3(1300) > Player2(1200)
        var finalRank1 = await _repository.GetUserDivisionRankAsync(TestShardId, TestPlayerId1, TestSeasonNumber);
        var finalRank2 = await _repository.GetUserDivisionRankAsync(TestShardId, TestPlayerId2, TestSeasonNumber);
        var finalRank3 = await _repository.GetUserDivisionRankAsync(TestShardId, TestPlayerId3, TestSeasonNumber);
        var finalUser3 = await _repository.GetUserRankAsync(TestShardId, TestPlayerId3, TestSeasonNumber);

        Assert.True(scoreUpdated3); // Player3分数已更新
        Assert.Equal(0, finalRank1); // Player1 仍然第1
        Assert.Equal(2, finalRank2); // Player2 降到第3
        Assert.Equal(1, finalRank3); // Player3 上升到第2
        Assert.NotNull(finalUser3);
        Assert.Equal(1300, finalUser3.HighestScore); // Player3分数已更新
        Assert.True(finalUser3.Win); // Player3 Win状态已更新

        _output.WriteLine($"多次分数上传测试完成: Player1从第3名上升到第1名，Player3从第2名通过分数提升保持在前2名");
    }

    [Fact]
    public async Task GetUserDivisionRankAsync_SameScoreDifferentTimestamp_RanksCorrectly()
    {
        // Arrange
        var division = 0;
        var groupId = 0L;
        var score = 1000L;
        var baseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 创建相同分数但不同时间戳的用户（更早的时间戳排名更高）
        var userRank1 = CreateTestUserRank(TestPlayerId1, TestSeasonNumber, division, score, true, groupId, baseTimestamp + 10); // 较晚
        var userRank2 = CreateTestUserRank(TestPlayerId2, TestSeasonNumber, division, score, true, groupId, baseTimestamp + 5);  // 较早

        _repository.AddUserRank(userRank1);
        _repository.AddUserRank(userRank2);
        await _dbContext.SaveChangesAsync();

        // Act
        var rank1 = await _repository.GetUserDivisionRankAsync(TestShardId, TestPlayerId1, TestSeasonNumber);
        var rank2 = await _repository.GetUserDivisionRankAsync(TestShardId, TestPlayerId2, TestSeasonNumber);

        // Assert
        // 相同分数时，时间戳较早的排名更高
        Assert.Equal(1, rank1); // TestPlayerId1 时间戳较晚，排名第2 (index=1)
        Assert.Equal(0, rank2); // TestPlayerId2 时间戳较早，排名第1 (index=0)
    }

    /// <summary>
    /// 模拟用户分数更新逻辑
    /// </summary>
    /// <param name="userRank">用户排名实体</param>
    /// <param name="newScore">新分数</param>
    /// <param name="newTimestamp">新时间戳</param>
    /// <param name="win">是否获胜</param>
    /// <returns>是否更新了最高分数</returns>
    private bool UpdateUserScore(UserRank userRank, long newScore, long newTimestamp, bool win)
    {
        bool scoreUpdated = false;

        // 只有更高分数才更新HighestScore
        if (newScore > userRank.HighestScore)
        {
            userRank.HighestScore = newScore;
            scoreUpdated = true;
        }

        // 总是更新时间戳和Win状态
        userRank.Timestamp = newTimestamp;
        userRank.Win = win;

        return scoreUpdated;
    }

    /// <summary>
    /// 创建测试用的用户排名数据
    /// </summary>
    private UserRank CreateTestUserRank(long playerId, int seasonNumber, int division, long highestScore,
        bool win, long groupId = 0L, long? timestamp = null)
    {
        return new UserRank
        {
            ShardId = TestShardId,
            PlayerId = playerId,
            SeasonNumber = seasonNumber,
            Division = division,
            GroupId = groupId,
            HighestScore = highestScore,
            Win = win,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }
}
