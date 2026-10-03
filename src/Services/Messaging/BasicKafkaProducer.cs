using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Messaging;

internal class BasicKafkaProducer : IDisposable
{
    private IProducer<string, byte[]> _producer;

    private readonly MessagingOptions _options;
    private readonly ILogger<BasicKafkaProducer> _logger;
    private readonly Lock _gate = new();
    private bool _disposed;

    public BasicKafkaProducer(MessagingOptions options, ILogger<BasicKafkaProducer> logger)
    {
        _options = options;
        _logger = logger;
        _producer = CreateProducer();
    }

    private IProducer<string, byte[]> CreateProducer()
    {
        return new ProducerBuilder<string, byte[]>(new ProducerConfig
            {
                BootstrapServers = _options.BootstrapServers,
                EnableIdempotence = _options.ProducerIdempotency,
                ClientId = _options.ProducerClientId,
                LingerMs = 5,
                MessageTimeoutMs = 90_000
            }).SetErrorHandler((_, e) => KafkaClientLogging.LogError(_logger, _options.ProducerClientId, e))
            .SetLogHandler((_, m) => KafkaClientLogging.LogMessage(_logger, m)).Build();
    }

    public async Task<ProduceResult> PublishAsync(string topic, Message<string, byte[]> message, CancellationToken ct)
    {
        IProducer<string, byte[]> producer;

        lock (_gate)
        {
            producer = _producer;
        }

        try
        {
            await producer.ProduceAsync(topic, message, ct);
            return new ProduceResult(true);
        }
        catch (ProduceException<string, byte[]> ex)
        {
            if (ex.Error.IsFatal)
            {
                _logger.LogCritical(ex, "Kafka producer hit a fatal error while publishing to {Topic}, recreating it",
                    topic);
                Replace(producer);
            }

            return new ProduceResult(false, ex.Error);
        }
        catch (OperationCanceledException)
        {
            return new ProduceResult(false, new Error(ErrorCode.Local_TimedOut, "Canceled"));
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
            _producer = CreateProducer();
        }

        broken.Flush(TimeSpan.FromSeconds(5));
        broken.Dispose();
    }
}

internal readonly record struct ProduceResult(bool Success, Error? Error = null);