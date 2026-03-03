using ChillyRoom.Infra.MQ;
using Confluent.Kafka;
using GameOutside.Models.Configs;
using Microsoft.Extensions.Options;

namespace GameOutside.Services.KafkaProducers;

public sealed class RefreshUserDivisionProducer : KafkaNullKeyProducer
{
    public RefreshUserDivisionProducer(IOptionsMonitor<KafkaConfig> kafkaConfigMonitor,
        ILogger<RefreshUserDivisionProducer> logger) : base(logger)
    {
        UpdateProducerConfig(
            kafkaConfigMonitor.CurrentValue.Brokers,
            kafkaConfigMonitor.CurrentValue.RefreshUserDivisionTopic);
        kafkaConfigMonitor.OnChange((c, _) =>
            UpdateProducerConfig(c.Brokers, c.RefreshUserDivisionTopic));
    }

    protected override void CustomizeProducerConfig(ProducerConfig config)
    {
        // 到达3次重试或达到超时时长10s，则发送失败
        config.MessageSendMaxRetries = 3;
        config.MessageTimeoutMs = 10000;
    }
}
