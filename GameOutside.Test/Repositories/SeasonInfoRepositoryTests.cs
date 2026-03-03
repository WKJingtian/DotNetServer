using GameOutside.Repositories;
using Xunit.Abstractions;

namespace GameOutside.Test.Repositories;

public class SeasonInfoRepositoryTests
{
    private readonly ITestOutputHelper _output;
    private readonly SeasonInfoRepository _repository;

    public SeasonInfoRepositoryTests(ITestOutputHelper output)
    {
        _output = output;
        _repository = new SeasonInfoRepository();
    }

    [Theory]
    [InlineData(0, 0)] // 开始时间
    [InlineData(1, 0)] // 1天后，仍在第0赛季
    [InlineData(2, 1)] // 2天后，第1赛季
    [InlineData(4, 2)] // 4天后，第2赛季
    [InlineData(6, 3)] // 6天后，第3赛季
    public async Task GetMockSeasonNumber_ShouldReturnCorrectSeasonNumber_ForDifferentDays(int daysAfterStart, int expectedSeason)
    {
        // Arrange
        var startSeason = new DateTime(2025, 7, 2, 11, 0, 0, DateTimeKind.Utc);
        var testTime = startSeason.AddDays(daysAfterStart);

        // Act
        var result = await _repository.GetCurrentSeasonNumberAsync();

        // Assert
        Assert.Equal(expectedSeason, result);
        _output.WriteLine($"测试时间: {testTime:yyyy-MM-dd HH:mm:ss} UTC, 预期赛季: {expectedSeason}, 实际赛季: {result}");
    }

    [Fact]
    public async Task GetCurrentSeasonNumberAsync_ShouldReturnConsistentResults_WhenCalledMultipleTimes()
    {
        // Act
        var result1 = await _repository.GetCurrentSeasonNumberAsync();
        await Task.Delay(10); // 短暂延迟
        var result2 = await _repository.GetCurrentSeasonNumberAsync();

        // Assert
        Assert.Equal(result1, result2);
        _output.WriteLine($"第一次调用结果: {result1}, 第二次调用结果: {result2}");
    }

    [Fact]
    public async Task GetMockSeasonNumber_ShouldHandleSeasonBoundary_Correctly()
    {
        // Arrange
        var startSeason = new DateTime(2025, 7, 2, 11, 0, 0, DateTimeKind.Utc);

        // 测试赛季边界时间点
        var testCases = new[]
        {
            new { Time = startSeason.AddSeconds(-1), ExpectedSeason = 0, Description = "赛季开始前1秒" },
            new { Time = startSeason, ExpectedSeason = 0, Description = "赛季开始时刻" },
            new { Time = startSeason.AddDays(2).AddSeconds(-1), ExpectedSeason = 0, Description = "第1赛季开始前1秒" },
            new { Time = startSeason.AddDays(2), ExpectedSeason = 1, Description = "第1赛季开始时刻" },
            new { Time = startSeason.AddDays(4).AddSeconds(-1), ExpectedSeason = 1, Description = "第2赛季开始前1秒" },
            new { Time = startSeason.AddDays(4), ExpectedSeason = 2, Description = "第2赛季开始时刻" },
            new { Time = startSeason.AddDays(6).AddSeconds(-1), ExpectedSeason = 2, Description = "第3赛季开始前1秒" },
            new { Time = startSeason.AddDays(6), ExpectedSeason = 3, Description = "第3赛季开始时刻" }
        };

        foreach (var testCase in testCases)
        {
            // Act
            var result = await _repository.GetCurrentSeasonNumberAsync();

            // Assert
            Assert.Equal(testCase.ExpectedSeason, result);
            _output.WriteLine($"{testCase.Description}: 时间={testCase.Time:yyyy-MM-dd HH:mm:ss} UTC, 预期={testCase.ExpectedSeason}, 实际={result}");
        }
    }
}
