namespace Messaging;

internal class MessagingOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to use auto-commit for kafka.
    /// </summary>
    internal bool UseAutoCommit { get; set; } = true;

    /// <summary>
    /// Gets or sets the auto-commit interval in milliseconds for kafka.
    /// </summary>
    internal int AutoCommitIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the connection string for the kafka.
    /// </summary>
    internal string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether to enable producer idempotency for kafka.
    /// </summary>
    internal bool ProducerIdempotency { get; set; } = true;

    /// <summary>
    /// Names of the topics to be used for messaging.
    /// </summary>
    internal TopicData[] Topics { get; set; } = Array.Empty<TopicData>();
}

public record struct TopicData(string Name, short ReplicationFactor, int NumPartitions);