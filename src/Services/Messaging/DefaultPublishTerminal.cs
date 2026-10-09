using Confluent.Kafka;
using Messaging.Abstractions;

namespace Messaging;

internal class DefaultPublishTerminal(BasicKafkaProducer producer, MessagingRegistry messagingRegistry)
    : IPublishTerminal
{
    public async Task<PublishOutcome> SendAsync<TMessage>(string topic, string key, string messageId, TMessage payload,
        IDictionary<string, string> headers, CancellationToken ct)
    {
        var kafkaHeaders = new Headers();

        foreach (var (headerKey, headerValue) in headers)
            kafkaHeaders.Add(headerKey, System.Text.Encoding.UTF8.GetBytes(headerValue));
        kafkaHeaders.Add(MessagingHeaders.EventType, System.Text.Encoding.UTF8.GetBytes(typeof(TMessage).Name));
        kafkaHeaders.Add(MessagingHeaders.MessageId, System.Text.Encoding.UTF8.GetBytes(messageId));

        var serializer = messagingRegistry.Messages[typeof(TMessage)].Serializer;
        var serializedPayload = serializer.Serialize(payload);
        if (!serializedPayload.Success) return new PublishOutcome(false, "Failed to serialize payload");

        var msg = new Message<string, byte[]>
        {
            Key = key,
            Value = serializedPayload.Message,
            Headers = kafkaHeaders
        };
        var (success, error) = await producer.PublishAsync(topic, msg, ct);
        return success ? new PublishOutcome(true) : new PublishOutcome(false, error?.Reason);
    }
}