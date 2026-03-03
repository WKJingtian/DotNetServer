namespace GameOutside.Models.Configs;

public class KafkaConfig
{
    public required string Brokers { get; set; }
    public required string RefreshUserDivisionTopic { get; set; }
    public required string RefreshUserDivisionDlqTopic { get; set; }
    public required string RefreshWorldRankTopic { get; set; }
    public required string RefreshWorldRankDlqTopic { get; set; }

    public required string DlqRetryTopicPrefix { get; set; } = "common.common-kafka-retry.dlq.";
}
