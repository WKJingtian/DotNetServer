using System.Text.Json;
using ChillyRoom.BuildingGame.Models;
using ChillyRoom.Infra.MQ;
using Confluent.Kafka;
using GameOutside.Models.Configs;
using Microsoft.Extensions.Options;

namespace GameOutside.Services.KafkaConsumers;

public class RefreshWorldRankConsumer : KafkaBackgroundConsumerWithDlq<RetryableKafkaMessage>
{
    private readonly IOptionsMonitor<KafkaConfig> _kafkaConfigMonitor;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<RefreshWorldRankConsumer> _logger;

    public RefreshWorldRankConsumer(
        IOptionsMonitor<KafkaConfig> kafkaConfigMonitor,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<RefreshWorldRankConsumer> logger) : base(logger, "building-game-refresh-world-rank", new ParallelFactory<Ignore>(logger))
    {
        _kafkaConfigMonitor = kafkaConfigMonitor;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;

        UpdateConsumerConfig(_kafkaConfigMonitor.CurrentValue.Brokers,
            _kafkaConfigMonitor.CurrentValue.RefreshWorldRankTopic, _kafkaConfigMonitor.CurrentValue.RefreshWorldRankDlqTopic);
        _kafkaConfigMonitor.OnChange(
            (c, _) =>
            {
                UpdateConsumerConfig(c.Brokers, c.RefreshWorldRankTopic, c.RefreshWorldRankDlqTopic);
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
        var msg = ((JsonElement)kafkaMsg.Message).Deserialize<RefreshWorldRankMessage>() ??
            throw new ArgumentException($"无法从 {kafkaMsg.Message} 获得 RefreshUserDivisionMessage");

        ValidateMessage(msg);

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var userInfoService = scope.ServiceProvider.GetRequiredService<UserInfoService>();
        var result = await userInfoService.UpdateWorldRankAsync(msg.PlayerId, msg.WorldRank,
            msg.SeasonToBeRefreshed);
        
        if (result == ErrorKind.SUCCESS)
        {
            _logger.LogInformation($"玩家 {msg.PlayerId} 的赛季 {msg.SeasonToBeRefreshed} 的世界排名{msg.WorldRank}结算成功");
        }
    }

    private void ValidateMessage(RefreshWorldRankMessage msg)
    {
        if (msg.SeasonToBeRefreshed <= 0)
        {
            throw new ArgumentException($"无效的赛季号 {msg.SeasonToBeRefreshed}");
        }

        if (msg.PlayerId <= 0)
        {
            throw new ArgumentException($"无效的 PlayerId {msg.PlayerId}");
        }

        if (msg.WorldRank < 0 || msg.WorldRank >= 100)
            throw new ArgumentException($"无效的 WorldRank {msg.WorldRank}");
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
