using Confluent.Kafka;
using Stock.Infrastructure.ExternalEvents;

namespace Stock.Infrastructure.Messaging.Publishers;

public interface IDeadLetterPublisher
{
    public Task PublishAsync<TValue>(string sourceTopic, TValue value, Exception exception, int attempt, CancellationToken ct)
        where TValue : IExternalEvent;

    public Task PublishAsync<TValue>(string sourceTopic, TValue value, bool isRetryable, int attempt,
        Exception? exception, CancellationToken ct) where TValue : IExternalEvent;

    public Task PublishFatalAsync<TValue>(string sourceTopic, TValue value, Exception exception, int attempt,
        CancellationToken ct) where TValue : IExternalEvent;

    public Task PublishPoisonAsync(string sourceTopic, Message<byte[], byte[]> message, Exception exception,
        CancellationToken ct);

    public Task PublishUnknownAsync<TValue>(TValue value, CancellationToken ct);
}
