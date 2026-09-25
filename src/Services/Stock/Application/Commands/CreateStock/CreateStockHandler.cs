using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Application.Commands.CreateStock;

public class CreateStockHandler(IStockWriter writer, IOutboxWriter outboxWriter, IUnitOfWorkDecorator uow)
    : IRequestHandler<CreateStockCommand, bool>
{
    public async Task<bool> Handle(CreateStockCommand request, CancellationToken cancellationToken)
    {
        var dto = new CreateStockDto(
            request.Name,
            request.IsOpenToTrade,
            request.TradingStartTime,
            request.TradingEndTime,
            request.Currency);

        var @event = new StockCreatedEvent(request.Id, request.Name, request.IsOpenToTrade, request.Currency,
            request.TradingStartTime, request.TradingEndTime);

        var created = false;

        await uow.ExecuteAsync(async () =>
        {
            created = await writer.CreateAsync(request.Id, dto, cancellationToken);
            if (!created) return;

            await outboxWriter.WriteAsync(@event, request.Id, cancellationToken);
        }, cancellationToken);

        return created;
    }
}
