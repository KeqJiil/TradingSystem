using Messaging.Abstractions;

namespace Messaging;

internal class MessagingRegistry
{
    public Dictionary<Type, (string Topic, IMessageSerializer Serializer)> Messages { get; } = new();
    public Dictionary<(string Topic, string ConsumerGroup), Dictionary<string, ConsumerBinding>> Consumers { get; } = new();
}

internal record struct ConsumerBinding(ConsumerOptions Options, DispatchDelegate Dispatch);

internal delegate Task<ConsumeOutcome> DispatchDelegate(IServiceProvider sp, RawMessage payload, CancellationToken ct);