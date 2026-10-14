using Microsoft.EntityFrameworkCore;

namespace Messaging.EFCore.Inbox;

internal class InboxCleanser<TContext>(TContext dbContext) where TContext : DbContext
{
    public async Task<int> CleanupAsync(int batchSize, DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        var deleted = 0;
        var newDeleted = 0;
        do
        {
            newDeleted = await dbContext.Set<InboxEntity>()
                .Where(x => x.CreatedAt < olderThan)
                .OrderBy(x => x.CreatedAt)
                .Take(batchSize)
                .ExecuteDeleteAsync(cancellationToken);
            deleted += newDeleted;
        } while (newDeleted == batchSize && !cancellationToken.IsCancellationRequested);

        return deleted;
    }
}