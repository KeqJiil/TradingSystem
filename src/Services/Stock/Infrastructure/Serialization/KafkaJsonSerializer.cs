using System.Text.Json;
using Confluent.Kafka;

namespace Stock.Infrastructure.Serialization;

public class KafkaJsonSerializer<T> : ISerializer<T>
{
    public byte[] Serialize(T data, SerializationContext context) =>
        JsonSerializer.SerializeToUtf8Bytes(data);
}

public class KafkaJsonDeserializer<T> : IDeserializer<T>
{
    public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context) =>
        JsonSerializer.Deserialize<T>(data)!;
}
