using Dapper;

namespace Messaging.Dapper.Outbox;

internal class OutboxCleaner(ITransactionContext dbContext)
{
    public async Task<int> CleanupAsync(int batchSize, DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        var sql = """
                    DELETE TOP (@BatchSize) FROM outbox
                    WHERE status = 'COMPLETED' AND created_at < @OlderThan
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