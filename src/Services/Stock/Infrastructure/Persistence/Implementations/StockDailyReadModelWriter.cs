using Dapper;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockDailyReadModelWriter(IDbContext dbContext) : IStockDailyReadModelWriter
{
    public async Task CreateDailyReadModelAsync(DailyReadModelAggregate aggregate, CancellationToken ct)
    {
        var sql = """
                  INSERT INTO daily_stock_data_projection
                      (id, aggregate_id, open_price, low_price, high_price, close_price, price_difference, last_version, date)
                  VALUES
                      (@Id, @AggregateId, @OpenPrice, @LowPrice, @HighPrice, @ClosePrice, @PriceDifference, @LastVersion, @Date)
                  """;

        await dbContext.EnsureConnectionOpenAsync(ct);

        await dbContext.Connection.ExecuteAsync(sql, new
        {
            Id = Guid.NewGuid(),
            aggregate.AggregateId,
            aggregate.OpenPrice,
            aggregate.LowPrice,
            aggregate.HighPrice,
            aggregate.ClosePrice,
            aggregate.PriceDifference,
            aggregate.LastVersion,
            aggregate.Date
        }, dbContext.Transaction);
    }
}
