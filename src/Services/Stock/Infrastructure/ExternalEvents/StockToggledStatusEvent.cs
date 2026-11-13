using Google.Protobuf.WellKnownTypes;
using Proto = TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Stock.Infrastructure.ExternalEvents;

public static class StockToggledStatusEventMapper
{
    public static Application.Events.StockToggledStatusEvent MapFrom(Proto.StockToggledStatusEvent @event)
    {
        return new Application.Events.StockToggledStatusEvent(Guid.Parse(@event.AggregateId),
            @event.ToggledAt.ToDateTimeOffset(), @event.IsOpenToTrade, @event.StatusVersion);
    }

    public static Proto.StockToggledStatusEvent MapToExternal(Application.Events.StockToggledStatusEvent @event)
    {
        return new Proto.StockToggledStatusEvent
        {
            AggregateId = @event.AggregateId.ToString(),
            ToggledAt = Timestamp.FromDateTimeOffset(@event.ToggledAt),
            IsOpenToTrade = @event.IsOpenToTrade,
            StatusVersion = @event.StatusVersion
        };
    }
}
