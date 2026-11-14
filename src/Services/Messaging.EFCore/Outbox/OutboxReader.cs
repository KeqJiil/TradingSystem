using System.Text.Json;
using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Messaging.EFCore.Outbox;

internal class OutboxReader<TContext>(TContext dbContext) where TContext : DbContext
{
    public async Task<IReadOnlyList<OutboxData>> GetPendingAsync(int amount, int leaseMinutes, CancellationToken ct)
    {
        var entities = await dbContext
            .Set<OutboxEntity>()
            .FromSqlRaw("""
                            ;WITH cte AS (
                                SELECT TOP({0}) o.*
                                FROM messaging.outbox AS o WITH (ROWLOCK, UPDLOCK, READPAST)
                                WHERE o.status IN ('Pending', 'Processing')
                                    AND (o.processed_at IS NULL OR o.processed_at <= DATEADD(MINUTE, -{1}, SYSDATETIMEOFFSET()))
                                    AND NOT EXISTS (
                                        SELECT 1
                                        FROM messaging.outbox AS earlier
                                        WHERE earlier.topic = o.topic
                                            AND earlier.message_key = o.message_key
                                            AND earlier.id < o.id
                                            AND earlier.status IN ('PENDING', 'PROCESSING'))
                                ORDER BY o.id
                            )
                            UPDATE cte
                            SET status = 'Processing', processed_at = SYSDATETIMEOFFSET(),
                                attempts = cte.attempts + 1
                            OUTPUT
                                inserted.*
                        """, amount, leaseMinutes).AsNoTracking().ToListAsync(ct);

        return entities.Select(e => new OutboxData(
                e.Id,
                e.MessageId,
                e.MessageType,
                e.Topic,
                e.MessageKey,
                e.Payload,
                JsonSerializer.Deserialize<Dictionary<string, string>>(e.Headers) ?? new Dictionary<string, string>(),
                e.Attempts,
                e.CreatedAt
            )
        ).ToList().AsReadOnly();
    }
}