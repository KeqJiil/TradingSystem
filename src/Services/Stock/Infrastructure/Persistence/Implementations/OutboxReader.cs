using Dapper;

namespace Stock.Infrastructure.Persistence.Implementations;

public class OutboxReader(IDbContext context) : IOutboxReader
{
    public async Task<IReadOnlyList<OutboxData>> GetPendingAsync(int amount, int maxWaitMinutes, CancellationToken ct)
    {
        var sql = """
                    ;WITH cte AS (
                        SELECT TOP(@Amount) *
                        FROM outbox WITH (READPAST)
                        WHERE (processed_at IS NULL OR DATEDIFF(MINUTE, processed_at, GETDATE()) > @MaxWaitMinutes) 
                            AND status != 'COMPLETED' AND attempts < 3
                            )
                    UPDATE cte
                    SET processed_at = GETDATE(), status = 'PROCESSING', attempts = cte.attempts + 1
                    OUTPUT 
                        inserted.id AS Id, inserted.aggregate_id AS AggregateId, inserted.event_type AS EventType,
                        inserted.status AS Status, inserted.payload AS Payload;
                  """;
        
        await context.EnsureConnectionOpenAsync(ct);

        var result = await context.Connection.QueryAsync<OutboxData>(sql, new { Amount = amount, MaxWaitMinutes = maxWaitMinutes }, context.Transaction);
        return result.ToList().AsReadOnly();
    }
};