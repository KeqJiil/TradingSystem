using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Application.Commands.CreateStock;

public class CreateStockHandler(IStockWriter writer, IOutboxWriter outboxWriter, IUnitOfWorkDecorator uow)
    : IRequestHandler<CreateStockCommand, Guid>
{
    public async Task<Guid> Handle(CreateStockCommand request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();

        var dto = new CreateStockDto(
            request.Name,
            request.IsOpenToTrade,
            request.TradingStartTime,
            request.TradingEndTime,
            request.Currency);

        await uow.ExecuteAsync(async () =>
        {
            await writer.CreateAsync(id, dto, cancellationToken);

            var @event = new StockCreatedEvent(id, request.Name, request.IsOpenToTrade, request.Currency,
                request.TradingStartTime, request.TradingEndTime);
            await outboxWriter.WriteAsync(@event, id, cancellationToken);
        }, cancellationToken);

        return id;
    }
}
