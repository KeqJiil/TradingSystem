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

public class KafkaProducerFactory(IOptions<KafkaOptions> options, ILogger<KafkaProducerFactory> logger)
    : IKafkaProducerFactory
{
    public IProducer<string, TValue> Create<TValue>(string clientId)
    {
        return Builder<TValue>(clientId)
            .SetValueSerializer(new ProtobufNetSerializer<TValue>())
            .Build();
    }

    public IProducer<string, TValue> CreateJson<TValue>(string clientId)
    {
        return Builder<TValue>(clientId)
            .SetValueSerializer(new KafkaJsonSerializer<TValue>())
            .Build();
    }

    public IProducer<string, byte[]> CreateRaw(string clientId)
    {
        return Builder<byte[]>(clientId).Build();
    }

    private ProducerBuilder<string, TValue> Builder<TValue>(string clientId)
    {
        return new ProducerBuilder<string, TValue>(new ProducerConfig
            {
                BootstrapServers = options.Value.BootstrapServers,
                ClientId = clientId,
                EnableIdempotence = true,
                LingerMs = 5,
                MessageTimeoutMs = 90_000
            })
            .SetErrorHandler((_, error) => KafkaClientLogging.LogError(logger, clientId, error))
            .SetLogHandler((_, message) => KafkaClientLogging.LogMessage(logger, message));
    }
}
