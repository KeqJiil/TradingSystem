using System.Runtime.CompilerServices;
using Dapper;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockEventStoreReader(IDbContext dbContext) : IStockEventStoreReader
{
    private const int PageSize = 200;

    public async IAsyncEnumerable<PriceChangedEvent> ListEventsAsync(Guid aggregateId, DateTimeOffset from,
        DateTimeOffset to, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var sql = """
                  SELECT TOP (@PageSize) s.aggregate_id AS StockId, s.price_change AS PriceChange,
                         s.created_at AS Timestamp, s.version AS Version
                  FROM events_store s
                  WHERE s.aggregate_id = @AggregateId
                    AND s.created_at >= @From AND s.created_at < @To
                    AND (@LastVersion IS NULL OR s.version > @LastVersion)
                  ORDER BY s.version ASC
                  """;

        await dbContext.EnsureConnectionOpenAsync(ct);

        long? lastVersion = null;

        while (true)
        {
            var page = (await dbContext.Connection.QueryAsync<EventRow>(
                sql,
                new { PageSize, AggregateId = aggregateId, From = from, To = to, LastVersion = lastVersion },
                dbContext.Transaction)).ToList();

            if (page.Count == 0) yield break;

            foreach (var row in page)
                yield return new PriceChangedEvent(row.StockId, row.PriceChange, row.Version);

            lastVersion = page[^1].Version;
        }
    }

    public async Task<long?> GetLastVersionAsync(Guid aggregateId, DateTimeOffset before, CancellationToken ct = default)
    {
        var sql = """
                  SELECT MAX(s.version)
                  FROM events_store s
                  WHERE s.aggregate_id = @AggregateId AND s.created_at < @Before
                  """;

        await dbContext.EnsureConnectionOpenAsync(ct);

        return await dbContext.Connection.ExecuteScalarAsync<long?>(
            sql, new { AggregateId = aggregateId, Before = before }, dbContext.Transaction);
    }

    private readonly record struct EventRow(Guid StockId, decimal PriceChange, DateTimeOffset Timestamp, long Version);
}
