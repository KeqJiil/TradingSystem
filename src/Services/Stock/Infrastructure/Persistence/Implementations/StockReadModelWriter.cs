using Dapper;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockReadModelWriter(IDbContext context) : IStockReadModelWriter
{
    public async Task<ReadModelUpdateOutcome> UpdateAsync(Guid aggregateId, long newVersion, decimal priceChange,
        CancellationToken ct)
    {
        await context.EnsureConnectionOpenAsync(ct);

        var currentVersion = await context.Connection.ExecuteScalarAsync<long>(
            """SELECT "version" FROM "stock_data_projection" WHERE "aggregate_id" = @AggregateId""",
            new { AggregateId = aggregateId }, context.Transaction);

        if (newVersion <= currentVersion) return ReadModelUpdateOutcome.Stale;
        if (newVersion > currentVersion + 1) return ReadModelUpdateOutcome.Gap;

        var sql = """
                    UPDATE "stock_data_projection"
                    SET "price" = "price" + @PriceChange, "version" = "version" + 1
                    WHERE "version" = @OldVersion AND "aggregate_id" = @AggregateId
                  """;

        var result = await context.Connection.ExecuteAsync(sql,
            new { OldVersion = newVersion - 1, PriceChange = priceChange, AggregateId = aggregateId },
            context.Transaction);

        return result > 0 ? ReadModelUpdateOutcome.Applied : ReadModelUpdateOutcome.Gap;
    }

    public async Task<bool> ReplayAsync(Guid aggregateId, long fromVersion, long toVersion, decimal priceChange,
        CancellationToken ct)
    {
        var sql = """
                    UPDATE "stock_data_projection"
                    SET "price" = "price" + @PriceChange, "version" = @ToVersion
                    WHERE "aggregate_id" = @AggregateId AND "version" = @FromVersion
                  """;

        await context.EnsureConnectionOpenAsync(ct);

        return await context.Connection.ExecuteAsync(sql,
            new
            {
                FromVersion = fromVersion, ToVersion = toVersion, PriceChange = priceChange, AggregateId = aggregateId
            },
            context.Transaction) > 0;
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
            AggregateId = data.AggregateId, Currency = data.Currency,
            Name = data.Name, IsOpenToTrade = data.IsOpenToTrade ? 1 : 0,
            TradingStart = data.TradingStartTime, TradingEnd = data.TradingCloseTime
        }, context.Transaction);

        return result > 0;
    }

    public async Task<bool> SetStatusAsync(Guid aggregateId, bool isOpenToTrade, long statusVersion,
        CancellationToken ct)
    {
        var sql = """
                    UPDATE "stock_data_projection"
                    SET "is_open_to_trade" = @IsOpenToTrade, "status_version" = @StatusVersion
                    WHERE "aggregate_id" = @AggregateId AND "status_version" < @StatusVersion;

                    SELECT CAST(CASE WHEN EXISTS (
                        SELECT 1 FROM "stock_data_projection" WHERE "aggregate_id" = @AggregateId
                    ) THEN 1 ELSE 0 END AS BIT);
                  """;

        await context.EnsureConnectionOpenAsync(ct);

        return await context.Connection.ExecuteScalarAsync<bool>(sql,
            new { AggregateId = aggregateId, IsOpenToTrade = isOpenToTrade, StatusVersion = statusVersion },
            context.Transaction);
    }
}