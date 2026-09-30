namespace Messaging.Abstractions;

public interface IMessageContext
{
    /// <summary>
    /// Message topic name.
    /// </summary>
    string Topic { get; }

    /// <summary>
    /// Unique message identifier.
    /// </summary>
    string MessageId { get; }

    /// <summary>
    /// Key for the message, used for partitioning in Kafka.
    /// </summary>
    string KeyId { get; }

    /// <summary>
    /// Message type name, used for deserialization.
    /// </summary>
    string MessageType { get; }

    /// <summary>
    /// Message payload in bytes.
    /// </summary>
    byte[] Message { get; }

    /// <summary>
    /// Timestamp when the message was produced.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Headers associated with the message, used for metadata and additional information.
    /// </summary>
    IDictionary<string, string> Headers { get; }
}