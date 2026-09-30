using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Stock.Infrastructure.Observability;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Publishers;

public sealed class KafkaPublisher(
    IKafkaProducerFactory producerFactory,
    IOptions<KafkaOptions> options,
    ILogger<KafkaPublisher> logger) : IKafkaPublisher, IDisposable
{
    private readonly Lock _gate = new();
    private IProducer<string, byte[]> _producer = producerFactory.CreateRaw(options.Value.ProducerClientId);
    private bool _disposed;

    public async Task PublishAsync(string topic, Message<string, byte[]> message, CancellationToken ct)
    {
        IProducer<string, byte[]> producer;

        lock (_gate)
        {
            producer = _producer;
        }

        try
        {
            await producer.ProduceTracedAsync(topic, message, ct);
        }
        catch (ProduceException<string, byte[]> ex) when (ex.Error.IsFatal)
        {
            logger.LogCritical(ex, "Kafka producer hit a fatal error while publishing to {Topic}, recreating it",
                topic);
            Replace(producer);
            throw;
        }
    }

    public void Dispose()
    {
        IProducer<string, byte[]> producer;

        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            producer = _producer;
        }

        producer.Flush(TimeSpan.FromSeconds(10));
        producer.Dispose();
    }

    private void Replace(IProducer<string, byte[]> broken)
    {
        lock (_gate)
        {
            if (_disposed || !ReferenceEquals(_producer, broken)) return;
            _producer = producerFactory.CreateRaw(options.Value.ProducerClientId);
        }

        broken.Flush(TimeSpan.FromSeconds(5));
        broken.Dispose();
    }
}
