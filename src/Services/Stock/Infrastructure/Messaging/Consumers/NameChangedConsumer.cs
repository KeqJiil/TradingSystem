using MediatR;
using Messaging.Abstractions;
using Stock.Application.Commands.UpdateNameReadModel;
using Stock.Application.Events;

namespace Stock.Infrastructure.Messaging.Consumers;

public class NameChangedConsumer(IMediator mediator) : IMessageConsumer<NameChangedEvent>
{
    public async Task<ConsumeOutcome> ConsumeAsync(NameChangedEvent message, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateNameReadModelCommand(message.AggregateId, message.Name, message.Version),
            cancellationToken);
        return new ConsumeOutcome(MessageConsumeResult.Success);
    }
}
