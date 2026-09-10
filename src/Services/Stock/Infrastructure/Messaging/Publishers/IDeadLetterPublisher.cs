using Stock.Infrastructure.ExternalEvents;

namespace Stock.Infrastructure.Messaging.Publishers;

public interface IDeadLetterPublisher
{
    public Task PublishAsync<TValue>(string sourceTopic, TValue value, Exception exception, int attempt, CancellationToken ct)
        where TValue : IExternalEvent;

    public Task PublishAsync<TValue>(string sourceTopic, TValue value, bool isRetryable, int attempt, CancellationToken ct) where TValue : IExternalEvent;

    public Task PublishUnknownAsync<TValue>(TValue value, CancellationToken ct);
}
