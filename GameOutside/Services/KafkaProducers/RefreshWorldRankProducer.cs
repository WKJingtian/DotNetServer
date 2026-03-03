using ChillyRoom.Infra.MQ;
using Confluent.Kafka;
using GameOutside.Models.Configs;
using Microsoft.Extensions.Options;

namespace GameOutside.Services.KafkaProducers;

public sealed class RefreshWorldRankProducer : KafkaNullKeyProducer
{
    public RefreshWorldRankProducer(IOptionsMonitor<KafkaConfig> kafkaConfigMonitor,
        ILogger<RefreshWorldRankProducer> logger) : base(logger)
    {
        UpdateProducerConfig(
            kafkaConfigMonitor.CurrentValue.Brokers,
            kafkaConfigMonitor.CurrentValue.RefreshWorldRankTopic);
        kafkaConfigMonitor.OnChange((c, _) =>
            UpdateProducerConfig(c.Brokers, c.RefreshWorldRankTopic));
    }

    protected override void CustomizeProducerConfig(ProducerConfig config)
    {
        // 到达3次重试或达到超时时长10s，则发送失败
        config.MessageSendMaxRetries = 3;
        config.MessageTimeoutMs = 10000;
    }
}