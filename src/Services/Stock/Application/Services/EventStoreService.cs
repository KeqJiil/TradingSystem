using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Application.Services;

public class EventStoreService
{
    private readonly IStockEventStore _stockEventStore;
    private readonly IUnitOfWorkDecorator _uow;
    private readonly IOutboxWriter _outboxWriter;
    
    public EventStoreService(IStockEventStore stockEventStore, IUnitOfWorkDecorator uow, IOutboxWriter outboxWriter)
    {
        _stockEventStore = stockEventStore;
        _uow = uow;
        _outboxWriter = outboxWriter;
    }

    public async Task<Guid> ChangePriceAppendAsync(ChangePrice request, CancellationToken ct)
    {
        var stockUpdateEvent = new PriceChangedEvent(request.AggregateId, request.PriceChange, null);
        
        await _uow.ExecuteAsync(async () =>
        {
            var @event = await _stockEventStore.AppendAsync(stockUpdateEvent, ct);
            await _outboxWriter.WriteAsync(@event, @event.AggregateId, ct);
        }, ct);
        
        return request.AggregateId;
    }
}

public record ChangePrice(
    Guid AggregateId,
    decimal PriceChange);