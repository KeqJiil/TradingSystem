namespace Stock.Infrastructure.ExternalEvents;

public record PriceChangedEvent(Guid AggregateId, decimal PriceChange, long? Version, DateTimeOffset OccuredAt);

public static class PriceChangedEventMapper
{
    public static Application.Events.PriceChangedEvent MapFrom(PriceChangedEvent @event)
    {
        return new Application.Events.PriceChangedEvent(
            @event.AggregateId,
            @event.PriceChange,
            @event.Version,
            @event.OccuredAt
        );
    }
}
