using Dapper;
using Messaging.Outbox;

namespace Messaging.Dapper.Outbox;

internal class OutboxReader(ITransactionContext context)
{
    public async Task<IReadOnlyList<OutboxData>> GetPendingAsync(int amount, int leaseMinutes, CancellationToken ct)
    {
        var sql = """
                    ;WITH cte AS (
                        SELECT TOP(@Amount) *
                        FROM outbox WITH (ROWLOCK, UPDLOCK, READPAST)
                        WHERE status IN ('PENDING', 'PROCESSING')
                            AND (processed_at IS NULL OR processed_at <= DATEADD(MINUTE, -@LeaseMinutes, SYSDATETIMEOFFSET()))
                        ORDER BY id
                            )
                    UPDATE cte
                    SET status = 'PROCESSING', processed_at = SYSDATETIMEOFFSET(),
                        attempts = cte.attempts + 1
                    OUTPUT
                        inserted.id AS Id, inserted.message_id AS MessageId, inserted.message_type AS MessageType,
                        inserted.topic AS Topic, inserted.message_key AS MessageKey, inserted.payload AS Payload,
                        inserted.headers AS Headers, inserted.attempts AS Attempts, inserted.created_at AS CreatedAt
                  """;

        await context.EnsureConnectionOpenAsync(ct);

        var rows = await context.Connection.QueryAsync<OutboxRow>(new CommandDefinition(sql,
            new { Amount = amount, LeaseMinutes = leaseMinutes }, context.Transaction, cancellationToken: ct));
        return rows.Select(r => r.ToData()).OrderBy(r => r.Id).ToList().AsReadOnly();
    }
}
