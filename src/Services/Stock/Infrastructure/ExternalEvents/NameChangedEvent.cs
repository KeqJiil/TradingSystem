using Proto = TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Stock.Infrastructure.ExternalEvents;

public static class NameChangedEventMapper
{
    public static Application.Events.NameChangedEvent MapFrom(Proto.NameChangedEvent @event)
    {
        return new Application.Events.NameChangedEvent(Guid.Parse(@event.AggregateId), @event.Name, @event.Version);
    }

    public static Proto.NameChangedEvent MapToExternal(Application.Events.NameChangedEvent @event)
    {
        return new Proto.NameChangedEvent
        {
            AggregateId = @event.AggregateId.ToString(),
            Name = @event.Name,
            Version = @event.Version
        };
    }
}
