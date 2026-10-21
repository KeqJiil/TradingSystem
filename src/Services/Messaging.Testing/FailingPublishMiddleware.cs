using Messaging.Abstractions;

namespace Messaging.Testing;

public sealed class FailingPublishMiddleware : IMessagePublishMiddleware
{
    public PublishDelegate OnPublishAsync<TMessage>(PublishContext<TMessage> context, PublishDelegate next,
        CancellationToken cancellationToken) =>
        (_, _) => throw new InvalidOperationException($"Publishing {typeof(TMessage).Name} failed.");
}
