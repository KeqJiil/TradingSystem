namespace Messaging.EFCore.Outbox;

internal class OutboxEntity
{
    public long Id { get; init; }
    public required string MessageId { get; init; }
    public required string MessageType { get; init; }
    public required string Topic { get; init; }
    public required string MessageKey { get; init; }
    public required byte[] Payload { get; init; }
    public required string Headers { get; init; }
    public OutboxStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    
    public OutboxEntity() { }
}

internal enum OutboxStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Dead = 3
}