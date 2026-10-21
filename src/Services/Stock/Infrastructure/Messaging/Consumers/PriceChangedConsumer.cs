using MediatR;
using Messaging.Abstractions;
using Stock.Application.Abstractions;
using Stock.Application.Commands.ReplayReadModel;
using Stock.Application.Commands.UpdateReadModel;
using Stock.Application.Events;

namespace Stock.Infrastructure.Messaging.Consumers;

public class PriceChangedConsumer(IMediator mediator) : IMessageConsumer<PriceChangedEvent>
{
    public async Task<ConsumeOutcome> ConsumeAsync(PriceChangedEvent message, CancellationToken cancellationToken)
    {
        if (message.Version is not { } version)
            return new ConsumeOutcome(MessageConsumeResult.DeadLetter, "PriceChangedEvent has no version");

        var outcome = await mediator.Send(
            new UpdateReadModelCommand(message.AggregateId, message.PriceChange, version), cancellationToken);

        if (outcome == ReadModelUpdateOutcome.Gap)
            await mediator.Send(new ReplayReadModelCommand(message.AggregateId, version), cancellationToken);

        return new ConsumeOutcome(MessageConsumeResult.Success);
    }
}
