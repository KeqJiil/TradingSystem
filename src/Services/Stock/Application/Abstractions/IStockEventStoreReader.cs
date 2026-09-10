using Stock.Application.Events;

namespace Stock.Application.Abstractions;

public interface IStockEventStoreReader
{
    IAsyncEnumerable<PriceChangedEvent> ListEventsAsync(Guid aggregateId, DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default);

    Task<IEnumerable<PriceChangedEvent>> ListEventsByVersionAsync(Guid aggregateId, long from, long to,
        CancellationToken ct = default);

    Task<long?> GetLastVersionAsync(Guid aggregateId, DateTimeOffset before, CancellationToken ct = default);
}