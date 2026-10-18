using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Messaging.EFCore.Outbox;

internal class OutboxStore<TContext>(OutboxWriter<TContext> writer, OutboxReader<TContext> reader)
    : IOutboxStore where TContext : DbContext
{
    public Task<IReadOnlyList<OutboxData>> GetPendingAsync(int amount, int leaseMinutes, CancellationToken ct)
    {
        return reader.GetPendingAsync(amount, leaseMinutes, ct);
    }

    public ValueTask MarkCompletedAsync(IReadOnlyList<long> completedIds, IReadOnlyList<long> deadIds,
        CancellationToken ct)
    {
        return writer.MarkCompletedAsync(completedIds, deadIds, ct);
    }
}