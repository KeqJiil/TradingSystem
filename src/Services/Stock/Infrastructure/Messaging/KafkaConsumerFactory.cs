using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Stock.Infrastructure.Serialization;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging;

public interface IKafkaConsumerFactory
{
    IConsumer<string, TValue> Create<TValue>(string groupId, string topic, string clientId);
}

public class KafkaConsumerFactory(IOptions<KafkaOptions> options, ILogger<KafkaConsumerFactory> logger)
    : IKafkaConsumerFactory
{
    public IConsumer<string, TValue> Create<TValue>(string groupId, string topic, string clientId)
    {
        var consumer = new ConsumerBuilder<string, TValue>(new ConsumerConfig
            {
                BootstrapServers = options.Value.BootstrapServers,
                GroupId = groupId,
                ClientId = clientId,
                EnableAutoCommit = true,
                AutoCommitIntervalMs = 1000,
                EnableAutoOffsetStore = false,
                AutoOffsetReset = AutoOffsetReset.Earliest
            })
            .SetValueDeserializer(new ProtobufNetDeserializer<TValue>())
            .SetErrorHandler((_, error) => KafkaClientLogging.LogError(logger, clientId, error))
            .SetLogHandler((_, message) => KafkaClientLogging.LogMessage(logger, message))
            .SetOffsetsCommittedHandler((_, committed) => KafkaClientLogging.LogCommitted(logger, clientId, committed))
            .Build();

        consumer.Subscribe(topic);

        return consumer;
    }
}
