namespace Messaging.Testing;

public sealed record OutboxSeed
{
    public string MessageId { get; init; } = Guid.NewGuid().ToString();
    public string MessageType { get; init; } = "TestMessage";
    public string Topic { get; init; } = "topic";
    public string Key { get; init; } = "key";
    public byte[] Payload { get; init; } = [1, 2, 3];
    public Dictionary<string, string> Headers { get; init; } = [];
    public string Status { get; init; } = "PENDING";
    public int Attempts { get; init; }
    public DateTimeOffset? ProcessedAt { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
}

public sealed class OutboxStoredRow
{
    public long Id { get; init; }
    public string MessageId { get; init; } = "";
    public string MessageType { get; init; } = "";
    public string Topic { get; init; } = "";
    public string MessageKey { get; init; } = "";
    public byte[] Payload { get; init; } = [];
    public string Headers { get; init; } = "";
    public string Status { get; init; } = "";
    public int Attempts { get; init; }
    public DateTimeOffset? ProcessedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public interface IOutboxTable
{
    Task ClearAsync();
    Task<long> SeedAsync(OutboxSeed seed);
    Task<OutboxStoredRow?> GetAsync(long id);
    Task<List<OutboxStoredRow>> GetAllAsync();
}
