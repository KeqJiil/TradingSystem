using Stock.Application.Events;

namespace Stock.Application.Abstractions;

public interface IDeadLetterPublisher
{
    public Task PublishAsync<TValue>(string sourceTopic, TValue value, Exception exception, CancellationToken ct)
        where TValue : BasicEvent;

    public Task PublishAsync<TValue>(string sourceTopic, TValue value, bool isRetryable, CancellationToken ct) where TValue : BasicEvent;

    public Task PublishUnknownAsync<TValue>(TValue value, CancellationToken ct);
}