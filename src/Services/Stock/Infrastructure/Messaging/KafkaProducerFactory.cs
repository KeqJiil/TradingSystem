using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Stock.Infrastructure.Serialization;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging;

public interface IKafkaProducerFactory
{
    IProducer<string, TValue> Create<TValue>(string clientId);

    IProducer<string, TValue> CreateJson<TValue>(string clientId);

    IProducer<string, byte[]> CreateRaw(string clientId);
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

    public IProducer<string, TValue> CreateJson<TValue>(string clientId) =>
        new ProducerBuilder<string, TValue>(new ProducerConfig
            {
                BootstrapServers = options.Value.BootstrapServers,
                ClientId = clientId,
            })
            .SetValueSerializer(new KafkaJsonSerializer<TValue>())
            .Build();

    public IProducer<string, byte[]> CreateRaw(string clientId) =>
        new ProducerBuilder<string, byte[]>(new ProducerConfig
            {
                BootstrapServers = options.Value.BootstrapServers,
                ClientId = clientId,
            })
            .Build();
}