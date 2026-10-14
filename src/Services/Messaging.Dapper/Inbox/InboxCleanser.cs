using Dapper;

namespace Messaging.Dapper.Inbox;

internal class InboxCleaner(ITransactionContext dbContext)
{
    public async Task<int> CleanupAsync(int batchSize, DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        var sql = """
                    DELETE TOP (@BatchSize) FROM inbox
                    WHERE created_at < @OlderThan
                  """;

        await dbContext.EnsureConnectionOpenAsync(cancellationToken);

        var total = 0;
        int deleted;

        do
        {
            deleted = await dbContext.Connection.ExecuteAsync(new CommandDefinition(sql,
                new { BatchSize = batchSize, OlderThan = olderThan }, dbContext.Transaction,
                cancellationToken: cancellationToken));
            total += deleted;
        } while (deleted == batchSize && !cancellationToken.IsCancellationRequested);

        return total;
    }
}