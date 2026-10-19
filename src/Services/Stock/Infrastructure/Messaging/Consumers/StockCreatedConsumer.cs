using MediatR;
using Messaging.Abstractions;
using Stock.Application.Commands.CreateReadModel;
using Stock.Application.Events;

namespace Stock.Infrastructure.Messaging.Consumers;

public class StockCreatedConsumer(IMediator mediator) : IMessageConsumer<StockCreatedEvent>
{
    public async Task<ConsumeOutcome> ConsumeAsync(StockCreatedEvent message, CancellationToken cancellationToken)
    {
        await mediator.Send(new CreateReadModelCommand(message.AggregateId, message.Name, message.IsOpenToTrade,
            message.Currency, message.TradingStartTime, message.TradingCloseTime), cancellationToken);
        return new ConsumeOutcome(MessageConsumeResult.Success);
    }
}
