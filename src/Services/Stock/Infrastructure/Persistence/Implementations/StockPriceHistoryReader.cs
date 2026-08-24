using Dapper;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockPriceHistoryReader(IDbContext dbContext) : IStockPriceHistoryReader
{
    public async Task<IEnumerable<PriceHistoryDateOnlyReadModel>> GetDailyPriceHistoryAsync(Guid stockId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var sql = """
                  SELECT
                   dp.price_difference AS Difference,
                   dp.low_price AS LowPrice,
                   dp.high_price AS HighPrice,
                   dp.open_price AS OpenPrice,
                   dp.close_price AS ClosePrice,
                   dp.date AS Date
                  FROM daily_stock_data_projection dp
                  WHERE dp.date < @To AND dp.date >= @From AND dp.aggregate_id = @StockId
                  ORDER BY dp.date DESC
                  """;

        await dbContext.EnsureConnectionOpenAsync(ct);

        return await dbContext.Connection.QueryAsync<PriceHistoryDateOnlyReadModel>(
            sql, 
            new { To = to, From = from, StockId = stockId }, 
            dbContext.Transaction);
    }

    public async Task<PriceHistoryDateOnlyReadModel?> GetDayPriceHistoryAsync(Guid stockId, DateOnly date, CancellationToken ct)
    {
        var sql = """
                  SELECT
                   dp.price_difference AS Difference,
                   dp.low_price AS LowPrice,
                   dp.high_price AS HighPrice,
                   dp.open_price AS OpenPrice,
                   dp.close_price AS ClosePrice,
                   dp.date AS Date
                  FROM daily_stock_data_projection dp
                  WHERE dp.date = @Date AND dp.aggregate_id = @StockId
                  """;
        
        await dbContext.EnsureConnectionOpenAsync(ct);
        
        return await dbContext.Connection.QuerySingleOrDefaultAsync<PriceHistoryDateOnlyReadModel>(
            sql, 
            new { Date = date, StockId = stockId }, 
            dbContext.Transaction);
    }

    public Task<IEnumerable<PriceHistoryReadModel>> GetPriceHistoryAsync(Guid stockId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
