using System.Text;
using System.Text.Json;
using Dapper;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockEventStore(IDbContext dbContext) : IStockEventStore
{
    public async Task AppendAsync(PriceChangeEvent stockEvent, CancellationToken ct)
    {
        await dbContext.EnsureConnectionOpenAsync(ct);

        await dbContext.Connection.ExecuteAsync("""
            INSERT INTO events_store (event_id, aggregate_id, version, event_type, payload, price_change)
            VALUES (@Id, @AggregateId, COALESCE((SELECT MAX(e.version) FROM events_store e WHERE e.aggregate_id = @AggregateId), 0) + 1, @EventType, @Payload, @PriceChange)
        """, 
            new { Id = Guid.NewGuid(), AggregateId = stockEvent.AggregateId,
                EventType = nameof(PriceChangeEvent), Payload = JsonSerializer.Serialize(stockEvent),
                PriceChange = stockEvent.PriceChange },
            dbContext.Transaction);
    }

    public async Task AppendAsync(IEnumerable<PriceChangeEvent> stockEvents, CancellationToken ct)
    {
        var events = stockEvents.ToList();
        if (events.Count == 0)
        {
            return;
        }

        await dbContext.EnsureConnectionOpenAsync(ct);

        var aggregateIds = events.Select(e => e.AggregateId).Distinct().ToList();

        var currentVersions = (await dbContext.Connection.QueryAsync<(Guid AggregateId, long Version)>("""
            SELECT aggregate_id AS AggregateId, MAX(version) AS Version
            FROM events_store
            WHERE aggregate_id IN @AggregateIds
            GROUP BY aggregate_id
        """, new { AggregateIds = aggregateIds }, dbContext.Transaction))
            .ToDictionary(x => x.AggregateId, x => x.Version);

        var parameters = new DynamicParameters();
        var sqlBuilder = new StringBuilder("INSERT INTO events_store (event_id, aggregate_id, version, event_type, payload, price_change) VALUES ");

        for (var i = 0; i < events.Count; i++)
        {
            var stockEvent = events[i];

            var version = currentVersions.GetValueOrDefault(stockEvent.AggregateId, 0) + 1;
            currentVersions[stockEvent.AggregateId] = version;

            if (i > 0) sqlBuilder.Append(", ");
            sqlBuilder.Append($"(@Id{i}, @AggregateId{i}, @Version{i}, @EventType{i}, @Payload{i}, @PriceChange{i})");
            
            parameters.Add($"Id{i}", Guid.NewGuid());
            parameters.Add($"AggregateId{i}", stockEvent.AggregateId);
            parameters.Add($"Version{i}", version);
            parameters.Add($"EventType{i}", nameof(PriceChangeEvent));
            parameters.Add($"Payload{i}", JsonSerializer.Serialize(stockEvent));
            parameters.Add($"PriceChange{i}", stockEvent.PriceChange);
        }

        await dbContext.Connection.ExecuteAsync(sqlBuilder.ToString(), parameters, dbContext.Transaction);
    }
}