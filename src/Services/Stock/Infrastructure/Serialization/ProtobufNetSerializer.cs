using System.Runtime.CompilerServices;
using Confluent.Kafka;
using ProtoBuf;
using ProtoBuf.Meta;
using SerializationContext = Confluent.Kafka.SerializationContext;

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

public class ProtobufNetSerializer<T> : ISerializer<T>
{
    public byte[] Serialize(T data, SerializationContext context)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, data);
        return stream.ToArray();
    }
}

public class ProtobufNetDeserializer<T> : IDeserializer<T>
{
    public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        using var stream = new MemoryStream(data.ToArray());
        return Serializer.Deserialize<T>(stream);
    }
}
