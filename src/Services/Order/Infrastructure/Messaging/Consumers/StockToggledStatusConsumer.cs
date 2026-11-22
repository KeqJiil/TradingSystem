using MediatR;
using Messaging.Abstractions;
using Order.Application.UseCases.SetStockStatus;
using TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Order.Infrastructure.Messaging.Consumers;

public class StockToggledStatusConsumer(IMediator mediator) : IMessageConsumer<StockToggledStatusEvent>
{
    public async Task<ConsumeOutcome> ConsumeAsync(StockToggledStatusEvent message,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message.AggregateId, out var stockId))
            return new ConsumeOutcome(MessageConsumeResult.DeadLetter, "Invalid stock id.");

        var result = await mediator.Send(
            new SetStockStatusCommand(stockId, message.IsOpenToTrade, message.StatusVersion), cancellationToken);
        return result.ToConsumeOutcome();
    }
}
