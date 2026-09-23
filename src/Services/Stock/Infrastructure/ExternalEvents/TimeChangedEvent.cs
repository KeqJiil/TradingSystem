namespace Stock.Infrastructure.ExternalEvents;

public record TimeChangedEvent(
    Guid AggregateId,
    TimeOnly TradingStartTime,
    TimeOnly TradingCloseTime,
    long Version) : IExternalEvent;

public static class TimeChangedEventMapper
{
    public static Application.Events.TimeChangedEvent MapFrom(TimeChangedEvent @event)
    {
        return new Application.Events.TimeChangedEvent(@event.AggregateId, @event.TradingStartTime,
            @event.TradingCloseTime, @event.Version);
    }

    public static TimeChangedEvent MapToExternal(Application.Events.TimeChangedEvent @event)
    {
        return new TimeChangedEvent(@event.AggregateId, @event.TradingStartTime, @event.TradingCloseTime,
            @event.Version);
    }
}