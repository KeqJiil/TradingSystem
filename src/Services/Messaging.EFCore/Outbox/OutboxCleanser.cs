using Microsoft.EntityFrameworkCore;

namespace Messaging.EFCore.Outbox;

internal class OutboxCleanser<TContext>(TContext dbContext) where TContext : DbContext
{
    public async Task<int> CleanupAsync(int batchSize, DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        var total = 0;
        int deleted;
        do
        {
            deleted = await dbContext.Set<OutboxEntity>().Where(x => x.Status == OutboxStatus.Completed && x.CreatedAt < olderThan)
                .Take(batchSize)
                .ExecuteDeleteAsync(cancellationToken);
            total += deleted;
        } while (deleted == batchSize && !cancellationToken.IsCancellationRequested);

        return total;
    }
}