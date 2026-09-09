using Stock.Application.Events;

namespace Stock.Application.Abstractions;

public interface IDeadLetterPublisher
{
    public Task PublishAsync<TValue>(string sourceTopic, TValue value, Exception exception, int attempt, CancellationToken ct)
        where TValue : BasicEvent;

    public Task PublishAsync<TValue>(string sourceTopic, TValue value, bool isRetryable, int attempt, CancellationToken ct) where TValue : BasicEvent;

    public Task PublishUnknownAsync<TValue>(TValue value, CancellationToken ct);
}