namespace Messaging.Abstractions;

public interface IMessageContext
{
    string Topic { get; }

    string MessageId { get; }
    string KeyId { get; }

    string Message { get; }

    DateTimeOffset Timestamp { get; }
    IDictionary<string, object> Headers { get; }
}