using Dapper;

namespace Messaging.Dapper.Outbox;

internal class OutboxReader(ITransactionContext context)
{
    public async Task<IReadOnlyList<OutboxData>> GetPendingAsync(int amount, int maxWaitMinutes, CancellationToken ct)
    {
        var sql = """
                    ;WITH cte AS (
                        SELECT TOP(@Amount) *
                        FROM outbox WITH (READPAST)
                        WHERE (processed_at IS NULL OR DATEDIFF(MINUTE, processed_at, SYSDATETIMEOFFSET()) > @MaxWaitMinutes)
                            AND status <> 'COMPLETED'
                        ORDER BY id
                            )
                    UPDATE cte
                    SET processed_at = SYSDATETIMEOFFSET(), status = 'PROCESSING', attempts = cte.attempts + 1
                    OUTPUT
                        inserted.id AS Id, inserted.message_id AS MessageId, inserted.message_type AS MessageType,
                        inserted.topic AS Topic, inserted.message_key AS MessageKey, inserted.payload AS Payload,
                        inserted.headers AS Headers, inserted.attempts AS Attempts, inserted.created_at AS CreatedAt
                  """;

        await context.EnsureConnectionOpenAsync(ct);

        var rows = await context.Connection.QueryAsync<OutboxRow>(new CommandDefinition(sql,
            new { Amount = amount, MaxWaitMinutes = maxWaitMinutes }, context.Transaction, cancellationToken: ct));
        return rows.Select(r => r.ToData()).ToList().AsReadOnly();
    }
}
