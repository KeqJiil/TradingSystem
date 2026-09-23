namespace Stock.Infrastructure.ExternalEvents;

public record NameChangedEvent(
    Guid AggregateId,
    string Name,
    long Version) : IExternalEvent;

public static class NameChangedEventMapper
{
    public static Application.Events.NameChangedEvent MapFrom(NameChangedEvent @event)
    {
        return new Application.Events.NameChangedEvent(@event.AggregateId, @event.Name, @event.Version);
    }

    public static NameChangedEvent MapToExternal(Application.Events.NameChangedEvent @event)
    {
        return new NameChangedEvent(@event.AggregateId, @event.Name, @event.Version);
    }
}