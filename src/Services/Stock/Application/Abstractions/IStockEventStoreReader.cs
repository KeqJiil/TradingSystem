using TradingSystem.Contracts.ProtobufClasses;

namespace Stock.Application.Abstractions;

public interface IStockEventStoreReader
{
    Task<IEnumerable<PriceChangedEvent>> ListEventsAsync(Guid aggregateId, DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct);
}