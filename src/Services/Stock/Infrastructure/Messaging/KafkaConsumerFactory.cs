using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Stock.Infrastructure.Serialization;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging;

public interface IKafkaConsumerFactory
{
    IConsumer<string, TValue> Create<TValue>(string groupId, string topic, string clientId);
}

public class KafkaConsumerFactory(IOptions<KafkaOptions> options) : IKafkaConsumerFactory
{
    public IConsumer<string, TValue> Create<TValue>(string groupId, string topic, string clientId)
    {
        var consumer = new ConsumerBuilder<string, TValue>(new ConsumerConfig
            {
                BootstrapServers = options.Value.BootstrapServers,
                GroupId = groupId,
                ClientId = clientId,
                EnableAutoCommit = false,
                EnableAutoOffsetStore = false,
                GroupProtocol = GroupProtocol.Consumer
            })
            .SetValueDeserializer(new ProtobufNetDeserializer<TValue>())
            .Build();

        consumer.Subscribe(topic);

        return consumer;
    }
}