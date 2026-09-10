using Stock.Application.Commands.ChangePrice;
using Stock.Application.Events;

namespace Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;

public class PriceChangeRequestedDlqEventToCommandMapper
    : IDlqEventToCommandMapper<PriceChangeRequested, ChangePriceCommand>
{
    public ChangePriceCommand Map(PriceChangeRequested message)
    {
        return new ChangePriceCommand(message.EventId, message.AggregateId, message.PriceChange, message.OccuredAt);
    }
}
