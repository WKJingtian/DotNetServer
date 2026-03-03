using System.Text.Json;
using ChillyRoom.Functions.DBModel;
using ChillyRoom.Infra.MQ;
using ChillyRoom.Infra.PlatformDef.DBModel.Models;
using Confluent.Kafka;
using GameOutside.DBContext;
using GameOutside.Models.Configs;
using Microsoft.Extensions.Options;

namespace GameOutside.Services.KafkaConsumers;

public class RefreshUserDivisionConsumer : KafkaBackgroundConsumerWithDlq<RetryableKafkaMessage>
{
    private readonly IOptionsMonitor<KafkaConfig> _kafkaConfigMonitor;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<RefreshUserDivisionConsumer> _logger;
    private readonly ServerConfigService _serverConfigService;

    public RefreshUserDivisionConsumer(
        IOptionsMonitor<KafkaConfig> kafkaConfigMonitor,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<RefreshUserDivisionConsumer> logger,
        ServerConfigService serverConfigService) : base(logger, "building-game-refresh-user-division", new ParallelFactory<Ignore>(logger))
    {
        _kafkaConfigMonitor = kafkaConfigMonitor;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _serverConfigService = serverConfigService;

        UpdateConsumerConfig(_kafkaConfigMonitor.CurrentValue.Brokers,
            _kafkaConfigMonitor.CurrentValue.RefreshUserDivisionTopic, _kafkaConfigMonitor.CurrentValue.RefreshUserDivisionDlqTopic);
        _kafkaConfigMonitor.OnChange(
            (c, _) =>
            {
                UpdateConsumerConfig(c.Brokers, c.RefreshUserDivisionTopic, c.RefreshUserDivisionDlqTopic);
            });
    }

    protected override RetryableKafkaMessage GetRetryableMessage(ConsumeResult<Ignore, string> cr)
    {
        var retryableMsg = JsonSerializer.Deserialize<RetryableKafkaMessage>(cr.Message.Value);
        if (retryableMsg == null || retryableMsg.Message == null)
        {
            _logger.LogError($"无法从 {cr.Message.Value} 获得 RetryableKafkaMessage");
            throw new Exception($"无法从 {cr.Message.Value} 获得 RetryableKafkaMessage");
        }
        return retryableMsg;
    }

    protected override async Task HandleMessage(IConsumer<Ignore, string> consumer, ConsumeResult<Ignore, string> cr, CancellationToken stoppingToken)
    {
        var kafkaMsg = GetRetryableMessage(cr);
        var msg = ((JsonElement)kafkaMsg.Message).Deserialize<RefreshUserDivisionMessage>() ??
            throw new ArgumentException($"无法从 {kafkaMsg.Message} 获得 RefreshUserDivisionMessage");

        ValidateMessage(msg);

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var divisionService = scope.ServiceProvider.GetRequiredService<DivisionService>();
        var dbCtx = scope.ServiceProvider.GetRequiredService<BuildingGameDB>();

        var result = await dbCtx.WithRCUDefaultRetry(async _ =>
        {
            var userDivision = await divisionService.GetUserDivisionAsync(msg.ShardId, msg.PlayerId, TrackingOptions.Tracking);
            if (userDivision == null || userDivision.LastSeasonNumber >= msg.SeasonToBeRefreshed)
            {
                return false;
            }

            userDivision.MaxDivisionScore = Math.Max(userDivision.DivisionScore, userDivision.MaxDivisionScore);
            userDivision.LastDivisionScore = userDivision.DivisionScore;
            userDivision.DivisionScore +=
                _serverConfigService.GetRewardsByRank(userDivision.DivisionScore, msg.Rank, msg.UsersCount);
            userDivision.LastDivisionRank = msg.Rank;
            userDivision.LastWorldRank = -1;
            userDivision.LastSeasonNumber = msg.SeasonToBeRefreshed;
            userDivision.RewardReceived = false;

            await dbCtx.SaveChangesWithDefaultRetryAsync();

            return true;
        });

        if (result)
        {
            _logger.LogInformation($"玩家 {msg.PlayerId} 的赛季 {msg.SeasonToBeRefreshed} 结算成功");
        }
    }

    private void ValidateMessage(RefreshUserDivisionMessage msg)
    {
        if (msg.SeasonToBeRefreshed <= 0)
        {
            throw new ArgumentException($"无效的赛季号 {msg.SeasonToBeRefreshed}");
        }

        if (msg.ShardId <= 0)
        {
            throw new ArgumentException($"无效的 ShardId {msg.ShardId}");
        }

        if (msg.PlayerId <= 0)
        {
            throw new ArgumentException($"无效的 PlayerId {msg.PlayerId}");
        }

        if (msg.UsersCount <= 0)
        {
            throw new ArgumentException($"无效的 UsersCount {msg.UsersCount}");
        }
    }

    protected override bool IsUnprocessableException(Exception e)
    {
        if (e is ArgumentException)
        {
            return true;
        }

        return false;
    }
}
