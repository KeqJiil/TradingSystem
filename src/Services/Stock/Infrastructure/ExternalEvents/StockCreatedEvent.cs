using Google.Protobuf.WellKnownTypes;
using Proto = TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Stock.Infrastructure.ExternalEvents;

public static class StockCreatedEventMapper
{
    public static Application.Events.StockCreatedEvent MapFrom(Proto.StockCreatedEvent @event)
    {
        return new Application.Events.StockCreatedEvent(
            Guid.Parse(@event.AggregateId),
            @event.Name,
            @event.IsOpenToTrade,
            @event.Currency,
            TimeOnly.FromTimeSpan(@event.TradingStartTime.ToTimeSpan()),
            TimeOnly.FromTimeSpan(@event.TradingCloseTime.ToTimeSpan())
        );
    }

    public static Proto.StockCreatedEvent MapToExternal(Application.Events.StockCreatedEvent @event)
    {
        return new Proto.StockCreatedEvent
        {
            AggregateId = @event.AggregateId.ToString(),
            Name = @event.Name,
            IsOpenToTrade = @event.IsOpenToTrade,
            Currency = @event.Currency,
            TradingStartTime = Duration.FromTimeSpan(@event.TradingStartTime.ToTimeSpan()),
            TradingCloseTime = Duration.FromTimeSpan(@event.TradingCloseTime.ToTimeSpan())
        };
    }
}
