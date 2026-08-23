using System.Text.Json;
using Dapper;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class OutboxWriter(IDbContext dbContext) : IOutboxWriter, IOutboxMarker
{
    public async Task WriteAsync<T>(T @event, Guid aggregateId, CancellationToken cancellationToken) where T : class

    {
        var id = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(@event);
        
        var sql = """
                    INSERT INTO outbox (id, event_type, payload, aggregate_id)
                    VALUES (@Id, @Type, @Payload, @AggregateId)
                  """;
        
        await dbContext.EnsureConnectionOpenAsync(cancellationToken);

        await dbContext.Connection.ExecuteAsync(sql, new { Id = id, Type = typeof(T).Name, Payload = payload, AggregateId = aggregateId }, dbContext.Transaction);
    }

    public async Task MarkCompletedAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var sql = """
                    UPDATE outbox
                    SET status = 'COMPLETED'
                    WHERE id IN (@Ids)
                  """;
        
        await dbContext.EnsureConnectionOpenAsync(cancellationToken);
        
        await dbContext.Connection.ExecuteAsync(sql, new { Ids = ids }, dbContext.Transaction);
    }
}