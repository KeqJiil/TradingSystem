namespace Stock.Infrastructure.ExternalEvents;

public record StockToggledStatusEvent(
    Guid AggregateId,
    DateTimeOffset ToggledAt,
    bool IsOpenToTrade,
    long StatusVersion) : IExternalEvent;

public static class StockToggledStatusEventMapper
{
    public static Application.Events.StockToggledStatusEvent MapFrom(StockToggledStatusEvent @event)
    {
        return new Application.Events.StockToggledStatusEvent(@event.AggregateId, @event.ToggledAt,
            @event.IsOpenToTrade, @event.StatusVersion);
    }

    public static StockToggledStatusEvent MapToExternal(Application.Events.StockToggledStatusEvent @event)
    {
        return new StockToggledStatusEvent(@event.AggregateId, @event.ToggledAt, @event.IsOpenToTrade,
            @event.StatusVersion);
    }
}
