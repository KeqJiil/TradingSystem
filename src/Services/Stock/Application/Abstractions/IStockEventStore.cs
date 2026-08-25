using Stock.Application.Events;

namespace Stock.Application.Abstractions;

public interface IStockEventStore
{
    public Task<PriceChangedEvent> AppendAsync(PriceChangedEvent stockEvent, CancellationToken ct);
    public Task<IEnumerable<PriceChangedEvent>> AppendAsync(IEnumerable<PriceChangedEvent> stockEvents, CancellationToken ct);
};