using System.Runtime.CompilerServices;
using Dapper;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockDataReader(IDbContext dbContext) : IStockDataReader
{
    public async IAsyncEnumerable<Guid> GetAllIdsAsync(int limit,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sql = """
                  SELECT TOP (@Limit) s.id AS AggregateId
                  FROM stock_data s
                  WHERE @LastAggregateId IS NULL OR s.id > @LastAggregateId
                  ORDER BY s.id ASC
                  """;

        await dbContext.EnsureConnectionOpenAsync(cancellationToken);

        Guid? lastAggregateId = null;

        while (true)
        {
            var page = (await dbContext.Connection.QueryAsync<StockIdRow>(
                sql,
                new { Limit = limit, LastAggregateId = lastAggregateId },
                dbContext.Transaction)).ToList();

            if (page.Count == 0) yield break;

            foreach (var row in page)
                yield return row.AggregateId;

            var last = page[^1];
            lastAggregateId = last.AggregateId;
        }
    }

    private readonly record struct StockIdRow(Guid AggregateId);
}