using MediatR;
using Messaging.Abstractions;
using Stock.Application.Commands.ChangePrice;
using Stock.Infrastructure.ExternalEvents;

namespace Stock.Infrastructure.Messaging.Consumers;

public class PriceChangeRequestedConsumer(IMediator mediator) : IMessageConsumer<PriceChangeRequestedEvent>
{
    public async Task<ConsumeOutcome> ConsumeAsync(PriceChangeRequestedEvent message,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new ChangePriceCommand(message.EventId, message.AggregateId, message.PriceChange,
            message.OccurredAt), cancellationToken);
        return new ConsumeOutcome(MessageConsumeResult.Success);
    }
}
