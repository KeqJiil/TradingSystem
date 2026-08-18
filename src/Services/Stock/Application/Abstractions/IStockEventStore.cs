namespace Stock.Application.Abstractions;

public interface IStockEventStore
{
    public Task AppendAsync(StockEvent stockEvent, CancellationToken ct);
    public Task AppendAsync(ICollection<StockEvent> stockEvents, CancellationToken ct);
};

public abstract record StockEvent();