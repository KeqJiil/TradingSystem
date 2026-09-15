using Confluent.Kafka;
using Stock.Infrastructure.Serialization;

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
                AutoOffsetReset = AutoOffsetReset.Earliest
            })
            .SetValueDeserializer(new ProtobufNetDeserializer<TValue>())
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
}
