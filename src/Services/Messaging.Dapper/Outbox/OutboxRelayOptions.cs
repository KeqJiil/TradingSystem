namespace Messaging.Dapper.Outbox;

public class OutboxRelayOptions
{
    /// <summary>
    /// Gets or sets how often the relay polls the outbox when there is nothing to drain.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets the number of rows claimed per batch.
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Gets or sets how many message keys are dispatched in parallel.
    /// </summary>
    public int MaxParallelKeys { get; set; } = 10;

    /// <summary>
    /// Gets or sets how long a claimed row is held before another relay instance may claim it again, in minutes.
    /// </summary>
    public int LeaseMinutes { get; set; } = 10;

    internal void Validate()
    {
        if (PollInterval <= TimeSpan.Zero)
            throw new InvalidOperationException($"{nameof(PollInterval)} must be greater than zero.");
        if (BatchSize < 1)
            throw new InvalidOperationException($"{nameof(BatchSize)} must be at least 1.");
        if (MaxParallelKeys < 1)
            throw new InvalidOperationException($"{nameof(MaxParallelKeys)} must be at least 1.");
        if (LeaseMinutes < 1)
            throw new InvalidOperationException($"{nameof(LeaseMinutes)} must be at least 1.");
    }
}
