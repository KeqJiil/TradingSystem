using Order.Infrastructure.Messaging.Serialization;
using Proto = TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Order.Infrastructure.Messaging.StockEvents;

public record StockCreatedEvent(
    Guid StockId,
    string Name,
    bool IsOpenToTrade,
    TimeOnly TradingStart,
    TimeOnly TradingEnd) : IProtobufParsable<StockCreatedEvent>
{
    public static StockCreatedEvent Parse(byte[] bytes)
    {
        var e = Proto.StockCreatedEvent.Parser.ParseFrom(bytes);
        return new StockCreatedEvent(Guid.Parse(e.AggregateId), e.Name, e.IsOpenToTrade,
            e.TradingStartTime.ToTimeOnly(), e.TradingCloseTime.ToTimeOnly());
    }
}
