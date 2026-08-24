using ProtoBuf;

namespace TradingSystem.Contracts.ProtobufClasses;

[ProtoContract]
public class PriceChangedEvent
{
    [ProtoMember(1)] public decimal PriceChange { get; set; }
    [ProtoMember(2)] public Guid StockId { get; set; }
    [ProtoMember(3)] public DateTimeOffset Timestamp { get; set; }
}