namespace Messaging;

public class MessagingOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to use auto-commit for kafka.
    /// </summary>
    public bool UseAutoCommit { get; set; } = true;

    /// <summary>
    /// Gets or sets the auto-commit interval in milliseconds for kafka.
    /// </summary>
    public int AutoCommitIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the connection string for the kafka.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether to enable producer idempotency for kafka.
    /// </summary>
    public bool ProducerIdempotency { get; set; } = true;

    /// <summary>
    /// Names of the topics to be used for messaging.
    /// </summary>
    public TopicData[] Topics { get; set; } = Array.Empty<TopicData>();

    public string BootstrapServers { get; set; } = string.Empty;

    public string ProducerClientId { get; set; } = string.Empty;
}

public record struct TopicData(string Name, short ReplicationFactor, int NumPartitions);