namespace Messaging.Outbox;

internal interface IOutboxStore
{
    Task<IReadOnlyList<OutboxData>> GetPendingAsync(int amount, int leaseMinutes, CancellationToken ct);

    ValueTask MarkCompletedAsync(IReadOnlyList<long> completedIds, IReadOnlyList<long> deadIds,
        CancellationToken ct);
}
