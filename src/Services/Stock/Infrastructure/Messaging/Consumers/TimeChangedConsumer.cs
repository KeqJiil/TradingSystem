using MediatR;
using Messaging.Abstractions;
using Stock.Application.Commands.UpdateTradingTimeReadModel;
using Stock.Application.Events;

namespace Stock.Infrastructure.Messaging.Consumers;

public class TimeChangedConsumer(IMediator mediator) : IMessageConsumer<TimeChangedEvent>
{
    public async Task<ConsumeOutcome> ConsumeAsync(TimeChangedEvent message, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateTradingTimeCommand(message.AggregateId, message.TradingStartTime,
            message.TradingCloseTime, message.Version), cancellationToken);
        return new ConsumeOutcome(MessageConsumeResult.Success);
    }
}
