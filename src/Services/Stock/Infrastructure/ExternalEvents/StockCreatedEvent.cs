namespace Stock.Infrastructure.ExternalEvents;

public record StockCreatedEvent(
    Guid AggregateId,
    string Name,
    bool IsOpenToTrade,
    string Currency,
    TimeOnly TradingStartTime,
    TimeOnly TradingCloseTime
) : IExternalEvent;

public static class StockCreatedEventMapper
{
    public static Application.Events.StockCreatedEvent MapFrom(StockCreatedEvent @event)
    {
        return new Application.Events.StockCreatedEvent(
            @event.AggregateId,
            @event.Name,
            @event.IsOpenToTrade,
            @event.Currency,
            @event.TradingStartTime,
            @event.TradingCloseTime
        );
    }

    public static StockCreatedEvent MapToExternal(Application.Events.StockCreatedEvent @event)
    {
        return new StockCreatedEvent(
            @event.AggregateId,
            @event.Name,
            @event.IsOpenToTrade,
            @event.Currency,
            @event.TradingStartTime,
            @event.TradingCloseTime
        );
    }
}
