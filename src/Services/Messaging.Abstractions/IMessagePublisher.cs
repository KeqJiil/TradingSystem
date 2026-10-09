namespace Messaging.Abstractions;

public interface IMessagePublisher
{
    Task<PublishOutcome> PublishAsync<TMessage>(TMessage message, PublishOptions? options,
        CancellationToken cancellationToken);
}

public record struct PublishOutcome(bool IsSuccessful, string? ErrorMessage = null);

public record struct PublishOptions(string KeyId, string Topic, Dictionary<string, string> Headers);