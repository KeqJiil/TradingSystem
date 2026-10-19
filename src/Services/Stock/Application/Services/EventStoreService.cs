using Messaging.Abstractions;
using Messaging.Dapper.Transactions;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Application.Services;

public class EventStoreService(IUnitOfWorkDecorator uow, IStockEventStore stockEventStore, IMessagePublisher publisher)
{
    public async Task<bool> ChangePriceAppendAsync(PriceChangeRequested request, CancellationToken ct)
    {
        var stored = false;

        await uow.ExecuteAsync(async () =>
        {
            var @event = await stockEventStore.AppendAsync(request, ct);
            stored = @event is not null;
            if (!stored) return;

            await publisher.PublishAsync(@event!, new PublishOptions { KeyId = @event!.AggregateId.ToString() }, ct);
        }, ct);

        return stored;
    }
}
