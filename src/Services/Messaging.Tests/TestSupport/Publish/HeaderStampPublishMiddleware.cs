namespace Messaging.Tests.TestSupport.Publish;

public sealed class HeaderStampPublishMiddleware : IMessagePublishMiddleware
{
    public const string HeaderName = "traceparent";
    public const string HeaderValue = "00-test-trace-01";

    public PublishDelegate OnPublishAsync<TMessage>(PublishContext<TMessage> context, PublishDelegate next,
        CancellationToken cancellationToken)
    {
        return (headers, token) =>
        {
            headers[HeaderName] = HeaderValue;
            return next(headers, token);
        };
    }
}
