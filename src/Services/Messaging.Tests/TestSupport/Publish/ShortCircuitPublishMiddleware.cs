namespace Messaging.Tests.TestSupport.Publish;

public sealed class ShortCircuitPublishMiddleware : IMessagePublishMiddleware
{
    public const string Error = "blocked";

    public PublishDelegate OnPublishAsync<TMessage>(PublishContext<TMessage> context, PublishDelegate next,
        CancellationToken cancellationToken)
    {
        return (_, _) => Task.FromResult(new PublishOutcome(false, Error));
    }
}
