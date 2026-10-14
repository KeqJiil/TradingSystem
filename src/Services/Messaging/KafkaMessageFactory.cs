using System.Text;
using Confluent.Kafka;
using Messaging.Abstractions;

namespace Messaging;

public static class KafkaMessageFactory
{
    public static Message<string, byte[]> Create(string key, byte[] payload, string messageType, string messageId,
        IEnumerable<KeyValuePair<string, string>> headers)
    {
        var kafkaHeaders = new Headers();

        foreach (var (headerKey, headerValue) in headers)
            kafkaHeaders.Add(headerKey, Encoding.UTF8.GetBytes(headerValue));
        kafkaHeaders.Add(MessagingHeaders.EventType, Encoding.UTF8.GetBytes(messageType));
        kafkaHeaders.Add(MessagingHeaders.MessageId, Encoding.UTF8.GetBytes(messageId));

        return new Message<string, byte[]>
        {
            Key = key,
            Value = payload,
            Headers = kafkaHeaders
        };
    }
}
