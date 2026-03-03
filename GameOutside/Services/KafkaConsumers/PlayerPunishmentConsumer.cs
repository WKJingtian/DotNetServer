using System.Text.Json;
using System.Text.Json.Serialization;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.GenericPlayerService.v1.Management;
using ChillyRoom.Infra.PlatformDef.Config;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using GameOutside.DBContext;
using GameOutside.Models;
using GenericPlayerManagementService.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace GameOutside.Services.KafkaConsumers;

public class PlayerPunishmentConsumer(GenericPlayerManagement.GenericPlayerManagementClient playerGmClient,
    IOptionsMonitor<PlatformKafkaConfig> kafkaConfigMonitor,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<PlayerPunishmentConsumer> logger) : PlayerPunishmentConsumerBase(playerGmClient, kafkaConfigMonitor, logger, "building-game.game.punishment")
{
    public const string RemoveFromLeaderboardRuleKey = "remove-from-the-leaderboard";
    public const string CleanUserAssetsRuleKey = "clean-user-assets";

    private static readonly HashSet<string> _endlessLeaderboardTypes = ["十面埋伏榜", "墨浪坚防榜", "无尽模式榜"];
    private static readonly HashSet<string> _supportedLeaderboardTypes = [.. _endlessLeaderboardTypes, "世界榜"];

    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

    protected override bool IsUnprocessableException(Exception e) => base.IsUnprocessableException(e);

    // 不可重试的错误通过返回值返回，可重试的错误抛出异常自动退避重试
    protected override async Task<(string, bool)> ProcessPlayerBannedAsync(PlayerStatusChangeEvent msg, CancellationToken stoppingToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var leaderboardModule = scope.ServiceProvider.GetRequiredService<LeaderboardModule>();
        var divisionService = scope.ServiceProvider.GetRequiredService<DivisionService>();
        var seasonService = scope.ServiceProvider.GetRequiredService<SeasonService>();
        var userRankService = scope.ServiceProvider.GetRequiredService<UserRankService>();
        var userEndlessRankService = scope.ServiceProvider.GetRequiredService<UserEndlessRankService>();
        var userAssetService = scope.ServiceProvider.GetRequiredService<UserAssetService>();
        var dbCtx = scope.ServiceProvider.GetRequiredService<BuildingGameDB>();

        return await dbCtx.WithRCUDefaultRetry(async _ =>
        {
            await using var transaction = await dbCtx.Database.BeginTransactionAsync();

            MarkTaskProcessed(dbCtx, msg);

            foreach (var rule in msg.Rules)
            {
                logger.LogInformation("QueueBanPlayerTask {TaskId} Player {PlayerId} banned, make punishment rule: {RuleKey} with params: {RuleParams}",
                    msg.TaskId, msg.Id, rule.Key, rule.Params);

                switch (rule.Key)
                {
                    case RemoveFromLeaderboardRuleKey:
                        var (leaderboardTypes, errorMessage) = ParseLeaderboardTypes(rule.Params);
                        if (!string.IsNullOrEmpty(errorMessage))
                        {
                            return (errorMessage, false);
                        }
                        await RemoveFromLeaderboardsAsync(
                            msg.ShardId,
                            msg.Id,
                            leaderboardTypes!,
                            divisionService,
                            seasonService,
                            userRankService,
                            userEndlessRankService,
                            leaderboardModule);
                        break;
                    case CleanUserAssetsRuleKey:
                        await CleanUserAssetsAsync(msg.ShardId, msg.Id, userAssetService);
                        break;
                    default:
                        return ($"未知的惩罚规则: {rule.Key}", false);
                }
            }

            try
            {
                await dbCtx.SaveChangesWithDefaultRetryAsync(false);
                await transaction.CommitAsync();
                dbCtx.ChangeTracker.AcceptAllChanges();
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
            {
                logger.LogWarning("PlayerPunishmentTask {TaskId} for Player {PlayerId} has already been processed, skipping duplicate processing",
                    msg.TaskId, msg.Id);
            }

            return (string.Empty, true);
        });
    }

    private void MarkTaskProcessed(BuildingGameDB dbCtx, PlayerStatusChangeEvent msg)
    {
        var record = new PlayerPunishmentTask
        {
            TaskId = msg.TaskId,
            ShardId = msg.ShardId,
            PlayerId = msg.Id
        };

        dbCtx.PlayerPunishmentTasks.Add(record);
    }

    private (HashSet<string>?, string?) ParseLeaderboardTypes(string rawParams)
    {
        try
        {
            var options = JsonSerializer.Deserialize<RemoveFromLeaderboardsParams>(rawParams);
            if (options == null || options.LeaderboardTypes.Count == 0)
            {
                return (null, "缺少排行榜类型参数");
            }

            var invalidTypes = options.LeaderboardTypes.Where(t => !_supportedLeaderboardTypes.Contains(t)).ToList();
            if (invalidTypes.Count > 0)
            {
                return (null, $"不支持的排行榜类型: {string.Join(", ", invalidTypes)}");
            }

            return ([.. options.LeaderboardTypes], null);
        }
        catch (JsonException e)
        {
            logger.LogError(e, "Failed to parse leaderboard types from params: {RawParams}", rawParams);
            return (null, "排行榜类型参数格式错误");
        }
    }

    private async Task RemoveFromLeaderboardsAsync(
        short shardId,
        long playerId,
        HashSet<string> leaderboardTypes,
        DivisionService divisionService,
        SeasonService seasonService,
        UserRankService userRankService,
        UserEndlessRankService userEndlessRankService,
        LeaderboardModule leaderboardModule)
    {
        var needEndlessRank = leaderboardTypes.Any(_endlessLeaderboardTypes.Contains);
        var endlessRank = needEndlessRank
            ? await userEndlessRankService.GetCurrentSeasonUserEndlessRankAsync(shardId, playerId)
            : null;

        foreach (var leaderboardType in leaderboardTypes)
        {
            switch (leaderboardType)
            {
                case "世界榜":
                {
                    var divisionNumber = await divisionService.GetDivisionNumberAsync(shardId, playerId, CreateOptions.DoNotCreateWhenNotExists);
                    var seasonNumber = seasonService.GetCurrentSeasonNumberByDivision(divisionNumber);
                    await userRankService.RemoveUserRankAsync(shardId, playerId, seasonNumber);
                    await leaderboardModule.RemovePlayerFromLeaderBoard(playerId, LeaderboardModule.NormalModeLeaderBoardId, seasonNumber);
                    break;
                }
                case "十面埋伏榜":
                {
                    if (endlessRank != null)
                    {
                        endlessRank.SurvivorScore = 0;
                    }
                    var currentSeasonNumber = seasonService.GetCurrentSeasonNumber();
                    await leaderboardModule.RemovePlayerFromLeaderBoard(playerId, LeaderboardModule.SurvivorModeLeaderBoardId, currentSeasonNumber);
                    break;
                }
                case "墨浪坚防榜":
                {
                    if (endlessRank != null)
                    {
                        endlessRank.TowerDefenceScore = 0;
                    }
                    var currentSeasonNumber = seasonService.GetCurrentSeasonNumber();
                    await leaderboardModule.RemovePlayerFromLeaderBoard(playerId, LeaderboardModule.TowerDefenceModeLeaderBoardId, currentSeasonNumber);
                    break;
                }
                case "无尽模式榜":
                {
                    if (endlessRank != null)
                    {
                        endlessRank.TrueEndlessScore = 0;
                    }
                    var currentSeasonNumber = seasonService.GetCurrentSeasonNumber();
                    await leaderboardModule.RemovePlayerFromLeaderBoard(playerId, LeaderboardModule.TrueEndlessModeLeaderBoardId, currentSeasonNumber);
                    break;
                }
                default:
                    logger.LogWarning("Unsupported leaderboard type {LeaderboardType} for player {PlayerId}", leaderboardType, playerId);
                    break;
            }
        }
    }

    private async Task CleanUserAssetsAsync(short shardId, long playerId, UserAssetService userAssetService)
    {
        var assets = await userAssetService.GetUserAssetsSimpleAsync(shardId, playerId);
        if (assets == null)
        {
            logger.LogWarning("Skip cleaning assets for non-existent player {PlayerId} in shard {ShardId}", playerId, shardId);
            return;
        }

        assets.CoinCount = 0;
        if (assets.DiamondCount > 0)
        {
            assets.DiamondCount = 0;
        }
    }
}

file class RemoveFromLeaderboardsParams
{
    [JsonPropertyName("leaderboard_types")]
    public List<string> LeaderboardTypes { get; set; } = [];
}
