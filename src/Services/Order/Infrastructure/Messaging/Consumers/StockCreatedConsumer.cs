using MediatR;
using Messaging.Abstractions;
using Order.Application.UseCases.RegisterStock;
using Order.Infrastructure.Messaging.StockEvents;

namespace Order.Infrastructure.Messaging.Consumers;

public class StockCreatedConsumer(IMediator mediator) : IMessageConsumer<StockCreatedEvent>
{
    public async Task<ConsumeOutcome> ConsumeAsync(StockCreatedEvent message, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new RegisterStockCommand(message.StockId, message.Name, message.IsOpenToTrade, message.TradingStart,
                message.TradingEnd), cancellationToken);
        return result.ToConsumeOutcome();
    }
}
