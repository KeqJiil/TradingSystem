using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Dapper;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class OutboxWriter(IDbContext dbContext) : IOutboxWriter, IOutboxMarker, IOutboxCleaner
{
    public async Task WriteAsync<T>(T @event, Guid aggregateId, CancellationToken cancellationToken) where T : class

    {
        var id = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(@event);

        var sql = """
                    INSERT INTO outbox (id, event_type, payload, aggregate_id, correlation_id, trace_parent, trace_state)
                    VALUES (@Id, @Type, @Payload, @AggregateId, @CorrelationId, @TraceParent, @TraceState)
                  """;

        await dbContext.EnsureConnectionOpenAsync(cancellationToken);

        await dbContext.Connection.ExecuteAsync(new CommandDefinition(sql,
            new
            {
                Id = id, Type = typeof(T).Name, Payload = payload, AggregateId = aggregateId,
                CorrelationId = CorrelationContext.CorrelationId,
                TraceParent = Activity.Current?.Id, TraceState = Activity.Current?.TraceStateString
            }, dbContext.Transaction, cancellationToken: cancellationToken));
    }

    public async Task WriteManyAsync<T>(IEnumerable<(Guid AggregateId, T Payload)> events,
        CancellationToken cancellationToken) where T : class
    {
        var sql = """
                        INSERT INTO outbox (id, event_type, payload, aggregate_id, correlation_id, trace_parent, trace_state)
                        SELECT id, event_type, payload, aggregate_id, correlation_id, trace_parent, trace_state
                        FROM @Events;
                  """;

        await dbContext.EnsureConnectionOpenAsync(cancellationToken);

        var table = new DataTable();
        table.Columns.Add("id", typeof(Guid));
        table.Columns.Add("event_type", typeof(string));
        table.Columns.Add("payload", typeof(string));
        table.Columns.Add("aggregate_id", typeof(Guid));
        table.Columns.Add("correlation_id", typeof(Guid));
        table.Columns.Add("trace_parent", typeof(string));
        table.Columns.Add("trace_state", typeof(string));

        var traceParent = Activity.Current?.Id;
        var traceState = Activity.Current?.TraceStateString;
        foreach (var (aggregateId, payload) in events)
            table.Rows.Add(Guid.NewGuid(), typeof(T).Name, JsonSerializer.Serialize(payload), aggregateId,
                CorrelationContext.CorrelationId, traceParent, traceState);

        var parameters = new DynamicParameters();
        parameters.Add("Events", table.AsTableValuedParameter("dbo.OutboxEventTvp"));

        await dbContext.Connection.ExecuteAsync(new CommandDefinition(sql, parameters, dbContext.Transaction, cancellationToken: cancellationToken));
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

        await dbContext.Connection.ExecuteAsync(new CommandDefinition(sql, new { Ids = ids }, dbContext.Transaction, cancellationToken: cancellationToken));
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
                new { BatchSize = batchSize, OlderThan = olderThan }, dbContext.Transaction, cancellationToken: cancellationToken));
            total += deleted;
        } while (deleted == batchSize && !cancellationToken.IsCancellationRequested);

        return total;
    }
}