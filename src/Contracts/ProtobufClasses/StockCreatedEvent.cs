using ProtoBuf;

namespace TradingSystem.Contracts.ProtobufClasses;

[ProtoContract]
public class StockCreatedEvent
{
    [ProtoMember(1)] public Guid AggregateId { get; set; }
    [ProtoMember(2)] public string Name { get; set; }
    [ProtoMember(3)] public bool IsOpenToTrade { get; set; }
    [ProtoMember(4)] public string Currency { get; set; }
    [ProtoMember(5)] public TimeOnly TradingStartTime { get; set; }
    [ProtoMember(6)] public TimeOnly TradingCloseTime { get; set; }
}