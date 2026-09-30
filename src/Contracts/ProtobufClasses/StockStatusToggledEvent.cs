using ProtoBuf;

namespace TradingSystem.Contracts.ProtobufClasses;

[ProtoContract]
public class StockStatusToggledEvent
{
    [ProtoMember(1)] public Guid AggregateId { get; set; }
    [ProtoMember(2)] public DateTimeOffset ToggledAt { get; set; }
}