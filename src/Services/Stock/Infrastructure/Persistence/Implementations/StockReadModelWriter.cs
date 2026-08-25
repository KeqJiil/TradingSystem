using Dapper;
using Polly;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockReadModelWriter(IDbContext context) : IStockReadModelWriter
{
    public async Task<bool> UpdateAsync(Guid aggregateId, long newVersion, decimal priceChange, CancellationToken ct)
    {
        var sql = """
                    UPDATE "stock_data_projection"
                    SET "price" = "price" + @PriceChange, "version" = "version" + 1
                    WHERE "version" = @OldVersion AND "aggregate_id" = @AggregateId
                  """;
        
        await context.EnsureConnectionOpenAsync(ct);

        var result = await context.Connection.ExecuteAsync(sql, new { OldVersion = newVersion - 1, AggregateId = aggregateId }, context.Transaction);

        return result > 0;
    }

    public async Task<bool> CreateAsync(CreateStockReadModelDto data, CancellationToken ct)
    {
        var sql = """
                  INSERT INTO "stock_data_projection" (
                                                       aggregate_id, name, is_open_to_trade, 
                                                       price, version, trading_start_time, 
                                                       trading_end_time, currency)
                  SELECT
                  @AggregateId, @Name,
                  @IsOpenToTrade, 0, 0,
                  @TradingStart, @TradingEnd,
                  @Currency
                  WHERE NOT EXISTS (
                    SELECT 1
                    FROM "stock_data_projection"
                    WHERE "aggregate_id" = @AggregateId
                  )
                  """;

        await context.EnsureConnectionOpenAsync(ct);

        var result = await context.Connection.ExecuteAsync(sql, new
        {
            AggregateId = data.aggregateId, Currency = data.currency,
            Name = data.name, IsOpenToTrade = data.isOpenToTrade,
            TradingStart = data.tradingStartTime, TradingEnd = data.tradingCloseTime
        }, context.Transaction);
        
        return result > 0;
    }

    public async Task<bool> ToggleStatusAsync(Guid aggregateId, CancellationToken ct)
    {
        var sql = """
                    UPDATE "stock_data_projection"
                    SET "is_open_to_trade" = NOT "is_open_to_trade" 
                    WHERE "aggregate_id" = @AggregateId
                  """;
        
        await context.EnsureConnectionOpenAsync(ct);

        var result = await context.Connection.ExecuteAsync(sql, new { AggregateId = aggregateId }, context.Transaction);
        
        return result > 0;
    }
}