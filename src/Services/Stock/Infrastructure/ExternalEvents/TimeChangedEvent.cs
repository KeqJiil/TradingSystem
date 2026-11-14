using Google.Protobuf.WellKnownTypes;
using Proto = TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Stock.Infrastructure.ExternalEvents;

public static class TimeChangedEventMapper
{
    public static Application.Events.TimeChangedEvent MapFrom(Proto.TimeChangedEvent @event)
    {
        return new Application.Events.TimeChangedEvent(Guid.Parse(@event.AggregateId),
            TimeOnly.FromTimeSpan(@event.TradingStartTime.ToTimeSpan()),
            TimeOnly.FromTimeSpan(@event.TradingCloseTime.ToTimeSpan()), @event.Version);
    }

    public static Proto.TimeChangedEvent MapToExternal(Application.Events.TimeChangedEvent @event)
    {
        return new Proto.TimeChangedEvent
        {
            AggregateId = @event.AggregateId.ToString(),
            TradingStartTime = Duration.FromTimeSpan(@event.TradingStartTime.ToTimeSpan()),
            TradingCloseTime = Duration.FromTimeSpan(@event.TradingCloseTime.ToTimeSpan()),
            Version = @event.Version
        };
    }
}
