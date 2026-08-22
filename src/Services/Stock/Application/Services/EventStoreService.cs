using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Application.Services;

public class EventStoreService
{
    private readonly IStockEventStore _stockEventStore;
    
    public EventStoreService(IStockEventStore stockEventStore)
    {
        _stockEventStore = stockEventStore;
    }

    public async Task<Guid> ChangePriceAppendAsync(ChangePrice request, CancellationToken cancellationToken)
    {
        var stockUpdateEvent = new PriceChangedEvent(request.AggregateId, request.PriceChange);
        await _stockEventStore.AppendAsync(stockUpdateEvent, cancellationToken);
        
        return request.AggregateId;
    }
}

public record ChangePrice(
    Guid AggregateId,
    decimal PriceChange);