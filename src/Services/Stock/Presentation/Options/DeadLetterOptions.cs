namespace Stock.Presentation.Options;

public static class DeadLetterOptions
{
    public const string Name = "DeadLetterOptions";

    public static string TopicSuffix { get; set; } = ".dlq";
    public static string UnknownTopic { get; set; } = "unknown-events.dlq";
    public static int MaxRetryAttempts { get; set; } = 5;
}