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
            await writer.ToggleOpenToTrade(request.Id, cancellationToken);

            var @event = new StockToggledStatusEvent(request.Id, DateTimeOffset.UtcNow);
            await outboxWriter.WriteAsync(@event, request.Id, cancellationToken);
        }, cancellationToken);
    }
}
