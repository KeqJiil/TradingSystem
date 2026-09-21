using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Application.Commands.ToggleStockOpenToTrade;

public class ToggleStockOpenToTradeHandler(IStockWriter writer, IOutboxWriter outboxWriter, IUnitOfWorkDecorator uow)
    : IRequestHandler<ToggleStockOpenToTradeCommand>
{
    public async Task Handle(ToggleStockOpenToTradeCommand request, CancellationToken cancellationToken)
    {
        await uow.ExecuteAsync(async () =>
        {
            var change = await writer.ToggleOpenToTrade(request.Id, cancellationToken);
            if (change is null) return;

            var @event = new StockToggledStatusEvent(request.Id, DateTimeOffset.UtcNow, change.IsOpenToTrade,
                change.StatusVersion);
            await outboxWriter.WriteAsync(@event, request.Id, cancellationToken);
        }, cancellationToken);
    }
}
