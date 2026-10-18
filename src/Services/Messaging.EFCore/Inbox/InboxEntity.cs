namespace Messaging.EFCore.Inbox;

internal class InboxEntity
{
    public required string ConsumerGroup { get; init; }
    public required string MessageId { get; init; }
    public DateTimeOffset CreatedAt { get; private set; }
    
    public InboxEntity() { }
}