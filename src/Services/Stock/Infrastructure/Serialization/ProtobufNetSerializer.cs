using System.Runtime.CompilerServices;
using ProtoBuf;
using ProtoBuf.Meta;

namespace Stock.Infrastructure.Serialization;

[ProtoContract]
internal readonly struct DateTimeOffsetSurrogate
{
    [ProtoMember(1)] public long DateTimeTicks { get; init; }
    [ProtoMember(2)] public long OffsetTicks { get; init; }

    public static implicit operator DateTimeOffsetSurrogate(DateTimeOffset value) =>
        new() { DateTimeTicks = value.DateTime.Ticks, OffsetTicks = value.Offset.Ticks };

    public static implicit operator DateTimeOffset(DateTimeOffsetSurrogate value) =>
        new(new DateTime(value.DateTimeTicks, DateTimeKind.Unspecified), new TimeSpan(value.OffsetTicks));
}

internal static class ProtobufNetModelConfiguration
{
    [ModuleInitializer]
    internal static void Configure()
    {
        RuntimeTypeModel.Default.Add(typeof(DateTimeOffset), false).SetSurrogate(typeof(DateTimeOffsetSurrogate));
    }
}
