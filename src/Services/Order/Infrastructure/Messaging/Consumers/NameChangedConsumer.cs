using MediatR;
using Messaging.Abstractions;
using Order.Application.UseCases.RenameStock;
using TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Order.Infrastructure.Messaging.Consumers;

public class NameChangedConsumer(IMediator mediator) : IMessageConsumer<NameChangedEvent>
{
    public async Task<ConsumeOutcome> ConsumeAsync(NameChangedEvent message, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(message.AggregateId, out var stockId))
            return new ConsumeOutcome(MessageConsumeResult.DeadLetter, "Invalid stock id.");

        var result = await mediator.Send(new RenameStockCommand(stockId, message.Name, message.Version),
            cancellationToken);
        return result.ToConsumeOutcome();
    }
}
