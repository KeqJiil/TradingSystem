using ProtoBuf;

namespace TradingSystem.Contracts.ProtobufClasses;

[ProtoContract]
public class StockStatusToggledEvent
{
    [ProtoMember(1)] public Guid AggregateId { get; set; }
    [ProtoMember(2)] public bool CurrentStatus { get; set; }
    [ProtoMember(3)] public DateTimeOffset Timestamp { get; set; }
}