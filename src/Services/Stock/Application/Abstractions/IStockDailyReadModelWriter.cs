using TradingSystem.Contracts.ProtobufClasses;

namespace Stock.Application.Abstractions;

public interface IStockDailyReadModelWriter
{
    Task CreateDailyReadModelAsync(DateTime date, IEnumerable<PriceChangedEvent> data, CancellationToken ct);
}