using System.ComponentModel.DataAnnotations;

namespace Stock.Infrastructure.Options;

public class DeadLetterOptions
{
    public const string Name = "DeadLetterOptions";

    [Required(AllowEmptyStrings = false)] public string TopicSuffix { get; set; } = ".dlq";

    [Required(AllowEmptyStrings = false)] public string UnknownTopic { get; set; } = "unknown-events.dlq";

    [Range(1, int.MaxValue)] public int MaxRetryAttempts { get; set; } = 5;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(10);
}