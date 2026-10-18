using Messaging.Outbox;

namespace Messaging.Dapper.Outbox;

internal class OutboxStore(OutboxReader reader, OutboxWriter writer) : IOutboxStore
{
    public Task<IReadOnlyList<OutboxData>> GetPendingAsync(int amount, int leaseMinutes, CancellationToken ct) =>
        reader.GetPendingAsync(amount, leaseMinutes, ct);

    public ValueTask MarkCompletedAsync(IReadOnlyList<long> completedIds, IReadOnlyList<long> deadIds,
        CancellationToken ct) =>
        writer.MarkCompletedAsync(completedIds, deadIds, ct);
}
