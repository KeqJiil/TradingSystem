using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockEventStore(IDbContext dbContext) : IStockEventStore
{
    public async Task<PriceChangedEvent?> AppendAsync(PriceChangeRequested stockEvent, CancellationToken ct)
    {
        try
        {
            var result = await AppendAsync([stockEvent], ct);
            return result.Single();
        }
        catch (SqlException ex) when (ex.Number == 2627)
        {
            return null;
        }
    }

    public async Task<IEnumerable<PriceChangedEvent>> AppendAsync(IEnumerable<PriceChangeRequested> stockEvents,
        CancellationToken ct)
    {
        var events = stockEvents.ToList();
        if (events.Count == 0) return new List<PriceChangedEvent>();

        await dbContext.EnsureConnectionOpenAsync(ct);

        var aggregateIds = events.Select(e => e.AggregateId).Distinct().ToList();

        var currentVersions = (await dbContext.Connection.QueryAsync<(Guid AggregateId, long Version)>(new CommandDefinition("""
                    SELECT aggregate_id AS AggregateId, MAX(version) AS Version
                    FROM events_store
                    WHERE aggregate_id IN @AggregateIds
                    GROUP BY aggregate_id
                """, new { AggregateIds = aggregateIds }, dbContext.Transaction, cancellationToken: ct)))
            .ToDictionary(x => x.AggregateId, x => x.Version);

        var parameters = new DynamicParameters();
        var sqlBuilder =
            new StringBuilder(
                "INSERT INTO events_store (event_id, aggregate_id, version, event_type, payload, price_change, occured_at) VALUES ");

        var result = new PriceChangedEvent[events.Count];

        for (var i = 0; i < events.Count; i++)
        {
            var stockEvent = events[i];

            var version = currentVersions.GetValueOrDefault(stockEvent.AggregateId, 0) + 1;
            currentVersions[stockEvent.AggregateId] = version;

            result[i] = new(stockEvent.AggregateId, stockEvent.PriceChange, version, stockEvent.OccuredAt);

            if (i > 0) sqlBuilder.Append(", ");
            sqlBuilder.Append(
                $"(@Id{i}, @AggregateId{i}, @Version{i}, @EventType{i}, @Payload{i}, @PriceChange{i}, @OccuredAt{i})");

            parameters.Add($"Id{i}", stockEvent.EventId);
            parameters.Add($"AggregateId{i}", stockEvent.AggregateId);
            parameters.Add($"Version{i}", version);
            parameters.Add($"EventType{i}", nameof(PriceChangedEvent));
            parameters.Add($"Payload{i}", JsonSerializer.Serialize(stockEvent));
            parameters.Add($"PriceChange{i}", stockEvent.PriceChange);
            parameters.Add($"OccuredAt{i}", stockEvent.OccuredAt);
        }

        await dbContext.Connection.ExecuteAsync(new CommandDefinition(sqlBuilder.ToString(), parameters, dbContext.Transaction, cancellationToken: ct));

        return result;
    }

    // Not Used
    private async Task<PriceChangedEvent> AppendAsyncLegacy(PriceChangedEvent stockEvent, CancellationToken ct)
    {
        await dbContext.EnsureConnectionOpenAsync(ct);

        var version = await dbContext.Connection.ExecuteAsync(new CommandDefinition(
            """
                INSERT INTO events_store (event_id, aggregate_id, version, event_type, payload, price_change)
                VALUES (@Id, @AggregateId, COALESCE((SELECT MAX(e.version) FROM events_store e WHERE e.aggregate_id = @AggregateId), 0) + 1, @EventType, @Payload, @PriceChange)
            """,
            new
            {
                Id = Guid.NewGuid(), AggregateId = stockEvent.AggregateId,
                EventType = nameof(PriceChangedEvent), Payload = JsonSerializer.Serialize(stockEvent),
                PriceChange = stockEvent.PriceChange
            }, dbContext.Transaction, cancellationToken: ct));

        return stockEvent with { Version = version };
    }
}