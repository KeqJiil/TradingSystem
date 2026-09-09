using Stock.Application.Events;

namespace Stock.Application.Abstractions;

public interface IStockEventStore
{
    public Task<PriceChangedEvent> AppendAsync(PriceChangeRequested stockEvent, CancellationToken ct);
    public Task<IEnumerable<PriceChangedEvent>> AppendAsync(IEnumerable<PriceChangeRequested> stockEvents, CancellationToken ct);
};