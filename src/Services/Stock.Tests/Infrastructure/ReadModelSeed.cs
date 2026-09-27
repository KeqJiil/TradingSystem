using Dapper;

namespace Stock.Tests.Infrastructure;

public static class ReadModelSeed
{
    public static Task Projection(TestDbContext dbContext, Guid aggregateId)
    {
        return dbContext.Connection.ExecuteAsync("""
            INSERT INTO stock_data_projection
                (aggregate_id, name, is_open_to_trade, price, version, trading_start_time, trading_end_time, currency)
            VALUES
                (@AggregateId, @Name, 1, 0, 0, @OpenTime, @CloseTime, @Currency)
            """,
            new
            {
                AggregateId = aggregateId,
                Name = "StockName",
                OpenTime = TimeSpan.FromHours(9),
                CloseTime = TimeSpan.FromHours(17),
                Currency = "USD"
            });
    }
}
