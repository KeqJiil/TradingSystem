namespace Messaging.Abstractions;

public sealed record DeliveryContext<TMessage>(
    string Topic, string ConsumerGroup, int Partition, long Offset, string MessageId, string MessageType,
    TMessage Message, DateTimeOffset Timestamp, Dictionary<string, string> Headers);

public sealed record PublishContext<TMessage>(
    string Topic, string KeyId, string MessageId, string MessageType,
    TMessage Message, Dictionary<string, string> Headers);