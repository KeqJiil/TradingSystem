namespace Messaging.Dlq;

internal interface IDeadLetterPublisher
{
    Task<bool> PublishAsync(RawMessage message, string reason, string? exceptionDetails, int attempt, CancellationToken ct);
}