using Confluent.Kafka;

namespace Stock.Tests.Infrastructure;

public static class KafkaTestConsumer
{
    public static ConsumeResult<string, TValue> WaitForMessage<TValue>(string bootstrapAddress, string topic,
        Func<TValue, bool> predicate, TimeSpan timeout) where TValue : class
    {
        using var consumer = new ConsumerBuilder<string, TValue>(new ConsumerConfig
            {
                BootstrapServers = bootstrapAddress,
                GroupId = $"test-observer-{Guid.NewGuid()}",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky
            })
            .SetValueDeserializer(new ProtobufDeserializer<TValue>())
            .Build();

        consumer.Subscribe(topic);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            ConsumeResult<string, TValue>? result;
            try
            {
                result = consumer.Consume(TimeSpan.FromSeconds(1));
            }
            catch (ConsumeException ex) when (ex.Error.Code is ErrorCode.UnknownTopicOrPart)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(500));
                continue;
            }

            if (result?.Message?.Value is not null && predicate(result.Message.Value))
            {
                consumer.Close();
                return result;
            }
        }

        consumer.Close();
        throw new TimeoutException($"No matching message observed on topic '{topic}' within {timeout}.");
    }

    private sealed class ProtobufDeserializer<T> : IDeserializer<T>
    {
        public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        {
            using var stream = new MemoryStream(data.ToArray());
            return ProtoBuf.Serializer.Deserialize<T>(stream);
        }
    }
}