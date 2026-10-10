namespace Messaging.Dapper;

public class CleansionOptions
{
    public int OutboxBatchSize { get; set; } = 200;
    public int InboxBatchSize { get; set; } = 200;
    public int InboxRetentionMinutes { get; set; } = 30 * 24 * 60;
    public int OutboxRetentionMinutes { get; set; } = 7 * 24 * 60;

    internal void Validate()
    {
        if (OutboxBatchSize < 1)
            throw new InvalidOperationException($"{nameof(OutboxBatchSize)} must be at least 1.");
        if (InboxBatchSize < 1)
            throw new InvalidOperationException($"{nameof(InboxBatchSize)} must be at least 1.");
        if (InboxRetentionMinutes < 1)
            throw new InvalidOperationException($"{nameof(InboxRetentionMinutes)} must be at least 1.");
        if (OutboxRetentionMinutes < 1)
            throw new InvalidOperationException($"{nameof(OutboxRetentionMinutes)} must be at least 1.");
    }
}