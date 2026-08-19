using Stock.Application.Events;

namespace Stock.Application.Abstractions;

public interface IStockEventStore
{
    public Task AppendAsync(PriceChangeEvent stockEvent, CancellationToken ct);
    public Task AppendAsync(IEnumerable<PriceChangeEvent> stockEvents, CancellationToken ct);
};