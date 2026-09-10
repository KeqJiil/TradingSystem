namespace Stock.Infrastructure.Options;

public class DeadLetterOptions
{
    public const string Name = "DeadLetterOptions";

    public string TopicSuffix { get; set; } = ".dlq";
    public string UnknownTopic { get; set; } = "unknown-events.dlq";
    public int MaxRetryAttempts { get; set; } = 5;
}
