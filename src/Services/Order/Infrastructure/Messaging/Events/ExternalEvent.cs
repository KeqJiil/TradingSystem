using Google.Protobuf;
using Messaging.Abstractions;

namespace Order.Infrastructure.Messaging.Events;

public sealed record ExternalEvent(
    Guid AggregateId,
    IMessage Message,
    Func<IMessagePublisher, PublishOptions, CancellationToken, Task<PublishOutcome>> Publish)
{
    public static ExternalEvent Create<T>(Guid aggregateId, T message) where T : IMessage<T> =>
        new(aggregateId, message, (publisher, options, ct) => publisher.PublishAsync(message, options, ct));
}
