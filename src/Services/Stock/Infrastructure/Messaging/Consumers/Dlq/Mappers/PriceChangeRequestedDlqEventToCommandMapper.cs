using Stock.Application.Commands.ChangePrice;
using Stock.Infrastructure.ExternalEvents;

namespace Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;

public class PriceChangeRequestedDlqEventToCommandMapper
    : IDlqEventToCommandMapper<PriceChangeRequestedEvent, ChangePriceCommand>
{
    public ChangePriceCommand Map(PriceChangeRequestedEvent message)
    {
        return new ChangePriceCommand(message.EventId, message.AggregateId, message.PriceChange, message.OccurredAt);
    }
}
