using MediatR;
using Messaging.Abstractions;
using Stock.Application.Commands.ToggleStatusReadModel;
using Stock.Application.Events;

namespace Stock.Infrastructure.Messaging.Consumers;

public class StockToggleStatusEventConsumer(IMediator mediator) : IMessageConsumer<StockToggledStatusEvent>
{
    public async Task<ConsumeOutcome> ConsumeAsync(StockToggledStatusEvent message,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new ToggleStatusReadModelCommand(message.AggregateId, message.IsOpenToTrade,
            message.StatusVersion), cancellationToken);
        return new ConsumeOutcome(MessageConsumeResult.Success);
    }
}
