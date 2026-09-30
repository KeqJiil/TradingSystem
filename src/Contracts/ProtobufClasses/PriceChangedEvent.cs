using ProtoBuf;

namespace TradingSystem.Contracts.ProtobufClasses;

[ProtoContract]
public class PriceChangedEvent
{
    [ProtoMember(1)] public decimal PriceChange { get; set; }
    [ProtoMember(2)] public Guid AggregateId { get; set; }
    [ProtoMember(3)] public long Version { get; set; }
    [ProtoMember(4)] public DateTimeOffset OccuredAt { get; set; }
}