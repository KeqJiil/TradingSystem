using Dapper;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockHourlyReadModelWriter(IDbContext dbContext) : IStockHourlyReadModelWriter
{
    public async Task CreateHourlyReadModelAsync(HourlyReadModelAggregate aggregate, CancellationToken ct)
    {
        var sql = """
                  INSERT INTO hourly_stock_data_projection 
                      (
                        id, aggregate_id, open_price, low_price, high_price, close_price, price_difference, last_version, date_time                               
                       ) SELECT
                      @EventId,
                      @Id,
                      @OpenPrice,
                      @LowPrice,
                      @HighPrice,
                      @ClosePrice,
                      @PriceDifference,
                      @LastVersion,
                      @HourStart
                  WHERE NOT EXISTS (
                      SELECT 1
                      FROM hourly_stock_data_projection
                      WHERE aggregate_id = @Id
                        AND date_time = @HourStart
                  );
                  """;

        await dbContext.EnsureConnectionOpenAsync(ct);

        await dbContext.Connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            EventId = Guid.NewGuid(),
            Id = aggregate.AggregateId,
            OpenPrice = aggregate.OpenPrice,
            LowPrice = aggregate.LowPrice,
            HighPrice = aggregate.HighPrice,
            ClosePrice = aggregate.ClosePrice,
            PriceDifference = aggregate.PriceDifference,
            HourStart = aggregate.HourStart,
            LastVersion = aggregate.LastVersion
        }, dbContext.Transaction, cancellationToken: ct));
    }
}