namespace Messaging.Tests.TestSupport.Publish;

public sealed class PublishContextLog
{
    public object? Last { get; set; }
}

public sealed class ContextCapturePublishMiddleware(PublishContextLog log) : IMessagePublishMiddleware
{
    public PublishDelegate OnPublishAsync<TMessage>(PublishContext<TMessage> context, PublishDelegate next,
        CancellationToken cancellationToken)
    {
        log.Last = context;
        return next;
    }
}
