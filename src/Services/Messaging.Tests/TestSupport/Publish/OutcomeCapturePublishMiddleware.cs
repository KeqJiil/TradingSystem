namespace Messaging.Tests.TestSupport.Publish;

public sealed class PublishOutcomeLog
{
    public List<PublishOutcome> Seen { get; } = new();
}

public sealed class OutcomeCapturePublishMiddleware(PublishOutcomeLog log) : IMessagePublishMiddleware
{
    public PublishDelegate OnPublishAsync<TMessage>(PublishContext<TMessage> context, PublishDelegate next,
        CancellationToken cancellationToken)
    {
        return async (headers, token) =>
        {
            var outcome = await next(headers, token);
            log.Seen.Add(outcome);
            return outcome;
        };
    }
}
