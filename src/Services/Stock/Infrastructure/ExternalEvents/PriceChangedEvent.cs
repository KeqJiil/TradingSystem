using System.Globalization;
using Google.Protobuf.WellKnownTypes;
using Proto = TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Stock.Infrastructure.ExternalEvents;

public static class PriceChangedEventMapper
{
    public static Application.Events.PriceChangedEvent MapFrom(Proto.PriceChangedEvent @event)
    {
        return new Application.Events.PriceChangedEvent(
            Guid.Parse(@event.AggregateId),
            decimal.Parse(@event.PriceChange, CultureInfo.InvariantCulture),
            @event.HasVersion ? @event.Version : null,
            @event.OccurredAt.ToDateTimeOffset()
        );
    }

    public static Proto.PriceChangedEvent MapToExternal(Application.Events.PriceChangedEvent @event)
    {
        var external = new Proto.PriceChangedEvent
        {
            AggregateId = @event.AggregateId.ToString(),
            PriceChange = @event.PriceChange.ToString(CultureInfo.InvariantCulture),
            OccurredAt = Timestamp.FromDateTimeOffset(@event.OccuredAt)
        };

        if (@event.Version is not null)
            external.Version = @event.Version.Value;

        return external;
    }
}
