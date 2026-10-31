using Microsoft.EntityFrameworkCore;

namespace Messaging.EFCore.Outbox;

internal class OutboxWriter<TContext>(TContext dbContext) where TContext : DbContext
{
    public void Write(OutboxEntity entry)
    {
        dbContext.Set<OutboxEntity>().Add(entry);
    }

    public void WriteMany(IReadOnlyCollection<OutboxEntity> entries)
    {
        dbContext.Set<OutboxEntity>().AddRange(entries);
    }

    public async ValueTask MarkCompletedAsync(IReadOnlyList<long> completedIds, IReadOnlyList<long> deadIds,
        CancellationToken cancellationToken)
    {
        if (completedIds.Count == 0 && deadIds.Count == 0) return;

        await dbContext.Set<OutboxEntity>().Where(x => completedIds.Contains(x.Id) || deadIds.Contains(x.Id))
            .ExecuteUpdateAsync(
                e => e.SetProperty(x => x.Status,
                    x => deadIds.Contains(x.Id) ? OutboxStatus.Dead : OutboxStatus.Completed),
                cancellationToken);
    }
}