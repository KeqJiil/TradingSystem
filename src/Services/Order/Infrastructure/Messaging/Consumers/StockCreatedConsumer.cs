using MediatR;
using Messaging.Abstractions;
using Order.Application.UseCases.RegisterStock;
using Order.Infrastructure.Messaging.Serialization;
using TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Order.Infrastructure.Messaging.Consumers;

public class StockCreatedConsumer(IMediator mediator) : IMessageConsumer<StockCreatedEvent>
{
    public async Task<ConsumeOutcome> ConsumeAsync(StockCreatedEvent message, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message.AggregateId, out var stockId))
            return new ConsumeOutcome(MessageConsumeResult.DeadLetter, "Invalid stock id.");

        if (!message.TradingStartTime.TryToTimeOnly(out var start) ||
            !message.TradingCloseTime.TryToTimeOnly(out var end))
            return new ConsumeOutcome(MessageConsumeResult.DeadLetter, "Invalid trading time.");

        var result = await mediator.Send(
            new RegisterStockCommand(stockId, message.Name, message.IsOpenToTrade, start, end), cancellationToken);
        return result.ToConsumeOutcome();
    }
}
