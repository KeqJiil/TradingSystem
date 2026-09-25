using Dapper;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockPriceHourlyReader(IDbContext dbContext) : IStockPriceHourlyReader
{
    public async Task<PriceHourReadModel?> GetHourPriceHistoryAsync(Guid stockId, DateTimeOffset dateTime,
        CancellationToken ct)
    {
        var sql = """
                    SELECT 
                        r.aggregate_id AS StockId,
                        r.open_price AS OpenPrice,
                        r.low_price AS LowPrice,
                        r.high_price AS HighPrice,
                        r.close_price AS ClosePrice,
                        r.date_time DateTime
                  FROM hourly_stock_data_projection AS r
                  WHERE r.aggregate_id = @StockId
                    AND r.date_time = @dateTime
                  """;

        await dbContext.EnsureConnectionOpenAsync(ct);

        var rows = await dbContext.Connection.QueryAsync<HourlyStockDataProjection>(new CommandDefinition(sql,
            new { StockId = stockId, dateTime }, dbContext.Transaction, cancellationToken: ct));

        var result = rows.Select(r => (HourlyStockDataProjection?)r).SingleOrDefault();
        if (result is null)
            return null;

        return new PriceHourReadModel(
            result.Value.StockId,
            result.Value.OpenPrice,
            result.Value.LowPrice,
            result.Value.HighPrice,
            result.Value.ClosePrice,
            result.Value.ClosePrice - result.Value.OpenPrice,
            result.Value.DateTime);
    }

    public async Task<IEnumerable<PriceHourReadModel>> GetHourlyPriceHistoryAsync(Guid stockId, DateTimeOffset from,
        DateTimeOffset to, CancellationToken ct)
    {
        var sql = """
                    SELECT 
                        r.aggregate_id AS StockId,
                        r.open_price AS OpenPrice,
                        r.low_price AS LowPrice,
                        r.high_price AS HighPrice,
                        r.close_price AS ClosePrice,
                        r.date_time DateTime
                  FROM hourly_stock_data_projection AS r
                  WHERE r.aggregate_id = @StockId
                    AND r.date_time >= @from
                    AND r.date_time < @to
                  ORDER BY r.date_time ASC
                  """;

        await dbContext.EnsureConnectionOpenAsync(ct);

        var result = await dbContext.Connection.QueryAsync<HourlyStockDataProjection>(new CommandDefinition(sql,
            new { StockId = stockId, from, to }, dbContext.Transaction, cancellationToken: ct));

        return result.Select(r => new PriceHourReadModel(
            r.StockId,
            r.OpenPrice,
            r.LowPrice,
            r.HighPrice,
            r.ClosePrice,
            r.ClosePrice - r.OpenPrice,
            r.DateTime));
    }

    public async Task<PriceHourReadModel?> GetLastHourPriceHistoryAsync(Guid stockId, DateTimeOffset before,
        CancellationToken ct)
    {
        var sql = """
                    SELECT TOP 1 
                        r.aggregate_id AS StockId,
                        r.open_price AS OpenPrice,
                        r.low_price AS LowPrice,
                        r.high_price AS HighPrice,
                        r.close_price AS ClosePrice,
                        r.date_time DateTime
                  FROM hourly_stock_data_projection AS r
                  WHERE r.aggregate_id = @StockId
                    AND r.date_time < @before
                  ORDER BY r.date_time DESC
                  """;

        await dbContext.EnsureConnectionOpenAsync(ct);

        var rows = await dbContext.Connection.QueryAsync<HourlyStockDataProjection>(new CommandDefinition(sql,
            new { StockId = stockId, before }, dbContext.Transaction, cancellationToken: ct));

        var result = rows.Select(r => (HourlyStockDataProjection?)r).SingleOrDefault();
        if (result is null)
            return null;

        return new PriceHourReadModel(
            result.Value.StockId,
            result.Value.OpenPrice,
            result.Value.LowPrice,
            result.Value.HighPrice,
            result.Value.ClosePrice,
            result.Value.ClosePrice - result.Value.OpenPrice,
            result.Value.DateTime);
    }

    private record struct HourlyStockDataProjection(
        Guid StockId,
        decimal OpenPrice,
        decimal LowPrice,
        decimal HighPrice,
        decimal ClosePrice,
        DateTimeOffset DateTime
    );
}