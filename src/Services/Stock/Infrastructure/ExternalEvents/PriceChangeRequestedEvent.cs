using Stock.Application.Events;

namespace Stock.Infrastructure.ExternalEvents;

public record PriceChangeRequestedEvent(
    Guid EventId,
    Guid AggregateId,
    decimal PriceChange,
    DateTimeOffset OccurredAt
);

public static class PriceChangeRequestedEventMapper
{
    public static PriceChangeRequested MapFrom(PriceChangeRequestedEvent @event)
    {
        return new PriceChangeRequested(
            @event.EventId,
            @event.AggregateId,
            @event.PriceChange,
            @event.OccurredAt
        );
    }
    
    public static IEnumerable<PriceChangeRequested> MapFrom(IEnumerable<PriceChangeRequestedEvent> events)
    {
        return events.Select(MapFrom);
    }
}