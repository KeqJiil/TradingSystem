using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Application.Commands.ToggleStockOpenToTrade;

public class ToggleStockOpenToTradeHandler(IStockWriter writer, IOutboxWriter outboxWriter, IUnitOfWorkDecorator uow)
    : IRequestHandler<ToggleStockOpenToTradeCommand, bool>
{
    public async Task<bool> Handle(ToggleStockOpenToTradeCommand request, CancellationToken cancellationToken)
    {
        var found = false;

        await uow.ExecuteAsync(async () =>
        {
            var change = await writer.ToggleOpenToTrade(request.Id, cancellationToken);
            found = change is not null;
            if (!found) return;

            var @event = new StockToggledStatusEvent(request.Id, DateTimeOffset.UtcNow, change!.IsOpenToTrade,
                change.StatusVersion);
            await outboxWriter.WriteAsync(@event, request.Id, cancellationToken);
        }, cancellationToken);

        return found;
    }
}
