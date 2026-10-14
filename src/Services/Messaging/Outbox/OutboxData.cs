namespace Messaging.Outbox;

internal record OutboxData(
    long Id,
    string MessageId,
    string MessageType,
    string Topic,
    string Key,
    byte[] Payload,
    IReadOnlyDictionary<string, string> Headers,
    int Attempts,
    DateTimeOffset CreatedAt);
