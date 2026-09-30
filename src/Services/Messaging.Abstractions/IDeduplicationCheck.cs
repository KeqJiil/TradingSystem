namespace Messaging.Abstractions;

public interface IDeduplicationCheck
{
    /// <summary>
    /// Checks if the message with the given key has already been processed.
    /// </summary>
    /// <param name="key">Dedup Key</param>
    /// <param name="consumerGroup">The consumer group to check for deduplication</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the message has been processed, false otherwise.</returns>
    Task<bool> CheckAsync(string key, string? consumerGroup, CancellationToken? cancellationToken);
}