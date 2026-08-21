using Dapper;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockReader(IDbContext dbContext) : IStockReader
{
    public async Task<StockReadModel> GetByIdAsync(Guid stockId, CancellationToken cancellationToken)
    {
        var sql = """
                  SELECT 
                      s.aggregate_id as AggregateId, 
                      s.currency as Currency, 
                      s.trading_start_time as TradingStartTime, 
                      s.trading_end_time as TradingEndTime, 
                      s.name as Name, 
                      s.is_open_to_trade as IsOpenToTrade, 
                      s.updated_at as UpdatedAt 
                  FROM stock_data_projection s 
                  WHERE s.aggregate_id = @StockId
                  """;

        await dbContext.EnsureConnectionOpenAsync(cancellationToken);

        return await dbContext.Connection.QuerySingleAsync<StockReadModel>(sql, new { StockId = stockId }, dbContext.Transaction);
    }
}