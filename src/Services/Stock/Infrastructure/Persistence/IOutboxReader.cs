namespace Stock.Infrastructure.Persistence;

public interface IOutboxReader
{
    public Task<IReadOnlyList<OutboxData>> GetPendingAsync(int amount, int maxWaitMinutes, CancellationToken cancellationToken);
}

public record struct OutboxData(Guid Id, Guid AggregateId, string EventType, string Status, string Payload);