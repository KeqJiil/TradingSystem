namespace Messaging.Abstractions;

public interface IPublishTerminal
{
    Task<PublishOutcome> SendAsync<TMessage>(string topic, string key, string messageId, TMessage payload,
        IDictionary<string, string> headers, CancellationToken ct);
}