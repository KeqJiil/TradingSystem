using MediatR;
using Messaging.Abstractions;
using Stock.Application.Commands.ChangePrice;
using Stock.Infrastructure.ExternalEvents;
using TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Stock.Infrastructure.Messaging.Consumers;

public class PriceChangeRequestedConsumer(IMediator mediator) : IMessageConsumer<PriceChangeRequestedEvent>
{
    public async Task<ConsumeOutcome> ConsumeAsync(PriceChangeRequestedEvent message,
        CancellationToken cancellationToken)
    {
        var request = PriceChangeRequestedEventMapper.MapFrom(message);
        await mediator.Send(new ChangePriceCommand(request.EventId, request.AggregateId, request.PriceChange,
            request.OccuredAt), cancellationToken);
        return new ConsumeOutcome(MessageConsumeResult.Success);
    }
}
