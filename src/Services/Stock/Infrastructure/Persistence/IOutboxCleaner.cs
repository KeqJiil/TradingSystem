namespace Stock.Infrastructure.Persistence;

public interface IOutboxCleaner
{
    Task<int> CleanupAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken);
}
