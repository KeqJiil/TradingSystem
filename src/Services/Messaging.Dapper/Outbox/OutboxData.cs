namespace Messaging.Dapper.Outbox;

internal record OutboxEntry(
    string MessageId,
    string MessageType,
    string Topic,
    string Key,
    byte[] Payload,
    IReadOnlyDictionary<string, string> Headers);

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

internal class OutboxRow
{
    public long Id { get; init; }
    public string MessageId { get; init; } = "";
    public string MessageType { get; init; } = "";
    public string Topic { get; init; } = "";
    public string MessageKey { get; init; } = "";
    public byte[] Payload { get; init; } = [];
    public string Headers { get; init; } = "{}";
    public int Attempts { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    public OutboxData ToData() => new(Id, MessageId, MessageType, Topic, MessageKey, Payload,
        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(Headers) ?? [], Attempts, CreatedAt);
}
