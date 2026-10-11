using Messaging.Abstractions;

namespace Messaging;

internal class DefaultPublishTerminal(BasicKafkaProducer producer, MessagingRegistry messagingRegistry)
    : IPublishTerminal
{
    public async Task<PublishOutcome> SendAsync<TMessage>(string topic, string key, string messageId, TMessage payload,
        IDictionary<string, string> headers, CancellationToken ct)
    {
        var serializer = messagingRegistry.Messages[typeof(TMessage)].Serializer;
        var serializedPayload = serializer.Serialize(payload);
        if (!serializedPayload.Success) return new PublishOutcome(false, "Failed to serialize payload");

        var msg = KafkaMessageFactory.Create(key, serializedPayload.Message, typeof(TMessage).Name, messageId, headers);
        var (success, error) = await producer.PublishAsync(topic, msg, ct);
        return success ? new PublishOutcome(true) : new PublishOutcome(false, error?.Reason);
    }
}
