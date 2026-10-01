namespace Messaging.Abstractions;

public enum ConsumerRetryMode
{
    Blocking,
    NonBlocking
}

public sealed record ConsumerOptions(string Topic, string ConsumerGroup)
{
    public ConsumerRetryMode RetryMode { get; init; } = ConsumerRetryMode.Blocking;
    public int MaxAttempts { get; init; } = 5;
}