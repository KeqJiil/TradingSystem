using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Application.Commands.CreateStock;

public class CreateStockHandler(
    IStockWriter writer,
    IStockDataReader reader,
    IOutboxWriter outboxWriter,
    IUnitOfWorkDecorator uow)
    : IRequestHandler<CreateStockCommand, CreateStockResult>
{
    public async Task<CreateStockResult> Handle(CreateStockCommand request, CancellationToken cancellationToken)
    {
        var dto = new CreateStockDto(
            request.Name,
            request.IsOpenToTrade,
            request.TradingStartTime,
            request.TradingEndTime,
            request.Currency);

        var @event = new StockCreatedEvent(request.Id, request.Name, request.IsOpenToTrade, request.Currency,
            request.TradingStartTime, request.TradingEndTime);

        var result = CreateStockResult.Created;

        await uow.ExecuteAsync(async () =>
        {
            if (await writer.CreateAsync(request.Id, dto, cancellationToken))
            {
                result = CreateStockResult.Created;
                await outboxWriter.WriteAsync(@event, request.Id, cancellationToken);
                return;
            }

            var existing = await reader.GetByIdAsync(request.Id, cancellationToken);
            result = IsExisting(existing, request);
        }, cancellationToken);

        return result;
    }

    private CreateStockResult IsExisting(StockMetadata? existing, CreateStockCommand request)
    {
        return existing is { } stock
               && stock.Name == request.Name
               && stock.IsOpenToTrade == request.IsOpenToTrade
               && stock.Currency == request.Currency
               && stock.TradingStartTime == request.TradingStartTime
               && stock.TradingEndTime == request.TradingEndTime
            ? CreateStockResult.AlreadyExists
            : CreateStockResult.Conflict;
    }
}
