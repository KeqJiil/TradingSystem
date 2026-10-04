namespace Messaging.Tests.TestSupport.Publish;

public sealed class EmptyPublishMiddleware : IMessagePublishMiddleware
{
    public PublishDelegate OnPublishAsync<TMessage>(PublishContext<TMessage> context,
        PublishDelegate next, CancellationToken cancellationToken)
    {
        return next;
    }
}
