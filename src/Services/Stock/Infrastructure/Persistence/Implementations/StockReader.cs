using Dapper;
using Messaging.Dapper;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockReader(ITransactionContext context) : IStockReader
{
    public async Task<StockReadModel?> GetByIdAsync(Guid stockId, CancellationToken cancellationToken)
    {
        var sql = """
                  SELECT 
                      s.aggregate_id as AggregateId, 
                      s.currency as Currency, 
                      s.trading_start_time as TradingStartTime, 
                      s.trading_end_time as TradingEndTime, 
                      s.name as Name, 
                      s.is_open_to_trade as IsOpenToTrade, 
                      s.price as Price,
                      s.updated_at as UpdatedAt 
                  FROM stock_data_projection s 
                  WHERE s.aggregate_id = @StockId
                  """;

        await context.EnsureConnectionOpenAsync(cancellationToken);

        var rows = await context.Connection.QueryAsync<StockReadModel>(new CommandDefinition(sql,
            new { StockId = stockId }, context.Transaction, cancellationToken: cancellationToken));

        return rows.Select(row => (StockReadModel?)row).SingleOrDefault();
    }

    public async Task<long?> GetVersionAsync(Guid aggregateId, CancellationToken cancellationToken = default)
    {
        var sql = """
                  SELECT s.version
                  FROM stock_data_projection s
                  WHERE s.aggregate_id = @AggregateId
                  """;

        await context.EnsureConnectionOpenAsync(cancellationToken);

        return await context.Connection.ExecuteScalarAsync<long?>(new CommandDefinition(sql,
            new { AggregateId = aggregateId }, context.Transaction, cancellationToken: cancellationToken));
    }
}