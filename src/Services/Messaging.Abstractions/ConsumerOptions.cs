namespace Messaging.Abstractions;

public sealed record ConsumerOptions(string Topic, string ConsumerGroup)
{
    public int? MaxAttempts { get; init; }
}
