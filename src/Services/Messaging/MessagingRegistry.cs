using Messaging.Abstractions;

namespace Messaging;

internal class MessagingRegistry
{
    public Func<IPublishContext, IServiceProvider, CancellationToken, Task> PublishPipeline;
    public Func<IDeliveryContext, IServiceProvider, CancellationToken, Task<ConsumeOutcome>> DeliveryPipeline;
    public Dictionary<string, (string Topic, IMessageSerializer Serializer)> Messages { get; } = new();
    public Dictionary<(string Topic, string ConsumerGroup), Dictionary<string, ConsumerBinding>> Consumers { get; } = new();
}

internal record struct ConsumerBinding(ConsumerOptions Options, DispatchDelegate Dispatch);

internal delegate Task<ConsumeOutcome> DispatchDelegate(IServiceProvider sp, byte[] payload, CancellationToken ct);