using System.Diagnostics;
using ChillyRoom.Games.BuildingGame.Services;
using GameOutside.Repositories;
using StackExchange.Redis;

namespace GameOutside.Services;

public class InitHostedService(
    ILogger<InitHostedService> logger,
    IServiceScopeFactory serviceScopeFactory)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(
            WarmupCache(cancellationToken),
            WarmupEfCore(cancellationToken),
            WarmupConfig(cancellationToken)
        );
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task WarmupEfCore(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();

            var seasonRefreshedHistoryRepository = scope.ServiceProvider.GetRequiredService<ISeasonRefreshedHistoryRepository>();

            sw.Stop();
            logger.LogInformation($"预热 EF Core 成功 in {sw.ElapsedMilliseconds} ms");
        }
        catch (Exception e)
        {
            sw.Stop();
            logger.LogWarning(e, $"预热 EF Core 失败 in {sw.ElapsedMilliseconds} ms");
            throw;
        }
    }

    private async Task WarmupCache(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();

            var cacheManager = scope.ServiceProvider.GetRequiredService<CacheManager>();
            var globalCache = scope.ServiceProvider.GetRequiredKeyedService<IConnectionMultiplexer>("GlobalCache");
            var leaderboardModule = scope.ServiceProvider.GetRequiredService<LeaderboardModule>();
            var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();

            sw.Stop();
            logger.LogInformation($"预热 cache 成功 in {sw.ElapsedMilliseconds} ms");
        }
        catch (Exception e)
        {
            sw.Stop();
            logger.LogWarning(e, $"预热 cache 失败 in {sw.ElapsedMilliseconds} ms");
            throw;
        }
    }

    private async Task WarmupConfig(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();

            var configService = scope.ServiceProvider.GetRequiredService<ServerConfigService>();

            sw.Stop();
            logger.LogInformation($"预热配置成功 in {sw.ElapsedMilliseconds} ms");
        }
        catch (Exception e)
        {
            sw.Stop();
            logger.LogWarning(e, $"预热配置失败 in {sw.ElapsedMilliseconds} ms");
            throw;
        }
    }
}
