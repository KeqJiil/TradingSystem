namespace Stock.Infrastructure.ExternalEvents;

public record StockToggledStatusEvent(Guid AggregateId, DateTimeOffset ToggledAt);

public static class StockToggledStatusEventMapper
{
    public static Application.Events.StockToggledStatusEvent MapFrom(StockToggledStatusEvent @event)
    {
        return new Application.Events.StockToggledStatusEvent(@event.AggregateId, @event.ToggledAt);
    }
}