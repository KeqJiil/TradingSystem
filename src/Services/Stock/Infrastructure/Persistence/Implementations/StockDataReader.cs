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
            var page = (await dbContext.Connection.QueryAsync<StockIdRow>(new CommandDefinition(
                sql,
                new { Limit = limit, LastAggregateId = lastAggregateId }, dbContext.Transaction, cancellationToken: cancellationToken))).ToList();

            if (page.Count == 0) yield break;

            foreach (var row in page)
                yield return row.AggregateId;

            var last = page[^1];
            lastAggregateId = last.AggregateId;
        }
    }

    public async Task<StockMetadata?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = """
                  SELECT
                      s.id AS Id,
                      s.name AS Name,
                      s.is_open_to_trade AS IsOpenToTrade,
                      s.trading_start_time AS TradingStartTime,
                      s.trading_end_time AS TradingEndTime,
                      s.currency AS Currency,
                      s.created_at AS CreatedAt
                  FROM stock_data s
                  WHERE s.id = @Id
                  """;

        await dbContext.EnsureConnectionOpenAsync(cancellationToken);

        var rows = await dbContext.Connection.QueryAsync<StockMetadata>(new CommandDefinition(sql, new { Id = id }, dbContext.Transaction, cancellationToken: cancellationToken));

        return rows.Select(row => (StockMetadata?)row).SingleOrDefault();
    }

    private readonly record struct StockIdRow(Guid AggregateId);
}