namespace Stock.Application.Abstractions;

public interface IStockReader
{
    public Task<StockReadModel> GetByIdAsync(Guid aggregateId, CancellationToken cancellationToken);
}

public record StockReadModel(Guid AggregateId, string Name, decimal Price, string Currency, bool IsOpenToTrade, TimeOnly TradingStartTime, TimeOnly TradingEndTime, DateTimeOffset UpdatedAt);