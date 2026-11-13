using System.Globalization;
using Stock.Application.Events;
using Proto = TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Stock.Infrastructure.ExternalEvents;

public static class PriceChangeRequestedEventMapper
{
    public static PriceChangeRequested MapFrom(Proto.PriceChangeRequestedEvent @event)
    {
        return new PriceChangeRequested(
            Guid.Parse(@event.EventId),
            Guid.Parse(@event.AggregateId),
            decimal.Parse(@event.PriceChange, CultureInfo.InvariantCulture),
            @event.OccurredAt.ToDateTimeOffset()
        );
    }
}
