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

    public async Task<bool> ChangePriceAppendAsync(PriceChangeRequested request, CancellationToken ct)
    {
        var stored = false;

        await _uow.ExecuteAsync(async () =>
        {
            var @event = await _stockEventStore.AppendAsync(request, ct);
            stored = @event is not null;
            if (!stored) return;

            await _outboxWriter.WriteAsync(@event!, @event!.AggregateId, ct);
        }, ct);

        return stored;
    }
}
