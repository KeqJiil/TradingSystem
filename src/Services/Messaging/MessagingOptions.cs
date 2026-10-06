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
    
    public bool EnableTopicRegistration { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of delivery attempts before a message is moved to the DLQ.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the pause before a failed message is read again.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the partition count for topics that are not listed in <see cref="Topics"/>.
    /// </summary>
    public int DefaultNumPartitions { get; set; } = 1;

    /// <summary>
    /// Gets or sets the replication factor for topics that are not listed in <see cref="Topics"/>.
    /// </summary>
    public short DefaultReplicationFactor { get; set; } = 1;

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(BootstrapServers))
            throw new InvalidOperationException($"{nameof(MessagingOptions)}.{nameof(BootstrapServers)} must be set.");
        if (MaxAttempts < 1)
            throw new InvalidOperationException($"{nameof(MaxAttempts)} must be at least 1.");
        if (RetryDelay < TimeSpan.Zero)
            throw new InvalidOperationException($"{nameof(RetryDelay)} must not be negative.");
        if (DefaultNumPartitions < 1)
            throw new InvalidOperationException($"{nameof(DefaultNumPartitions)} must be at least 1.");
        if (DefaultReplicationFactor < 1)
            throw new InvalidOperationException($"{nameof(DefaultReplicationFactor)} must be at least 1.");
    }
}

public record struct TopicData(string Name, short ReplicationFactor, int NumPartitions);