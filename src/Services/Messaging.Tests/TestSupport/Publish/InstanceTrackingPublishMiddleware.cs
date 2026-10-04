namespace Messaging.Tests.TestSupport.Publish;

public sealed class PublishInstanceLog
{
    public List<object> Instances { get; } = new();
}

public sealed class InstanceTrackingPublishMiddleware : IMessagePublishMiddleware
{
    public InstanceTrackingPublishMiddleware(PublishInstanceLog log)
    {
        log.Instances.Add(this);
    }

    public PublishDelegate OnPublishAsync<TMessage>(PublishContext<TMessage> context, PublishDelegate next,
        CancellationToken cancellationToken)
    {
        return next;
    }
}
