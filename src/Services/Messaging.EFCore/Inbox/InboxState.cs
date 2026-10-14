namespace Messaging.EFCore.Inbox;

internal class InboxState
{
    public string? MessageId { get; private set; } = null;
    public string? ConsumerGroup { get; private set; } = null;
    public bool Committed { get; private set; } = false;
    public bool Added { get; set; } = false;

    public void Set(string messageId, string consumerGroup)
    {
        MessageId = messageId;
        ConsumerGroup = consumerGroup;
    }

    public void Commit()
    {
        Committed = true;
    }
}