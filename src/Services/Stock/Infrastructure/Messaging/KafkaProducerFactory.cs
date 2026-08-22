using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Stock.Infrastructure.Serialization;
using Stock.Presentation.Options;

namespace Stock.Infrastructure.Messaging;

public interface IKafkaProducerFactory
{
    IProducer<string, TValue> Create<TValue>(string clientId);
}

public class KafkaProducerFactory(IOptions<KafkaOptions> options) : IKafkaProducerFactory
{
    public IProducer<string, TValue> Create<TValue>(string clientId) =>
        new ProducerBuilder<string, TValue>(new ProducerConfig
            {
                BootstrapServers = options.Value.BootstrapServers,
                ClientId = clientId,
            })
            .SetValueSerializer(new ProtobufNetSerializer<TValue>())
            .Build();
}