using System.Data;
using System.Text.Json;
using Dapper;

namespace Messaging.Dapper.Outbox;

internal class OutboxWriter(ITransactionContext dbContext)
{
    public async Task WriteAsync(OutboxEntry entry, CancellationToken cancellationToken)
    {
        var sql = """
                    INSERT INTO outbox (id, message_id, message_type, topic, message_key, payload, headers)
                    VALUES (@Id, @MessageId, @MessageType, @Topic, @MessageKey, @Payload, @Headers)
                  """;

        await dbContext.EnsureConnectionOpenAsync(cancellationToken);

        await dbContext.Connection.ExecuteAsync(new CommandDefinition(sql,
            new
            {
                Id = Guid.NewGuid(),
                entry.MessageId,
                entry.MessageType,
                entry.Topic,
                MessageKey = entry.Key,
                entry.Payload,
                Headers = JsonSerializer.Serialize(entry.Headers)
            }, dbContext.Transaction, cancellationToken: cancellationToken));
    }

    public async Task WriteManyAsync(IReadOnlyCollection<OutboxEntry> entries, CancellationToken cancellationToken)
    {
        if (entries.Count == 0) return;

        var sql = """
                    INSERT INTO outbox (id, message_id, message_type, topic, message_key, payload, headers, attempts)
                    SELECT id, message_id, message_type, topic, message_key, payload, headers, attempts
                    FROM @Events;
                  """;

        var table = new DataTable();
        table.Columns.Add("id", typeof(Guid));
        table.Columns.Add("message_id", typeof(string));
        table.Columns.Add("message_type", typeof(string));
        table.Columns.Add("topic", typeof(string));
        table.Columns.Add("message_key", typeof(string));
        table.Columns.Add("payload", typeof(byte[]));
        table.Columns.Add("headers", typeof(string));
        table.Columns.Add("attempts", typeof(int));

        foreach (var entry in entries)
            table.Rows.Add(Guid.NewGuid(), entry.MessageId, entry.MessageType, entry.Topic, entry.Key, entry.Payload,
                JsonSerializer.Serialize(entry.Headers), 0);

        await dbContext.EnsureConnectionOpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("Events", table.AsTableValuedParameter("dbo.outbox_event_tvp"));

        await dbContext.Connection.ExecuteAsync(new CommandDefinition(sql, parameters, dbContext.Transaction,
            cancellationToken: cancellationToken));
    }

    public async ValueTask MarkCompletedAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return;

        var sql = """
                    UPDATE outbox
                    SET status = 'COMPLETED'
                    WHERE id IN @Ids
                  """;

        await dbContext.EnsureConnectionOpenAsync(cancellationToken);

        await dbContext.Connection.ExecuteAsync(new CommandDefinition(sql, new { Ids = ids }, dbContext.Transaction,
            cancellationToken: cancellationToken));
    }

    public async Task<int> CleanupAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
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
