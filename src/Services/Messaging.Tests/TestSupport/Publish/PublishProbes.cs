namespace Messaging.Tests.TestSupport.Publish;

public abstract class PublishProbe(string name, TraceLog log) : IMessagePublishMiddleware
{
    public PublishDelegate OnPublishAsync<TMessage>(PublishContext<TMessage> context, PublishDelegate next,
        CancellationToken cancellationToken)
    {
        return async (headers, token) =>
        {
            log.Entries.Add($"{name}:before");
            var outcome = await next(headers, token);
            log.Entries.Add($"{name}:after");
            return outcome;
        };
    }
}

public sealed class PublishProbeA(TraceLog log) : PublishProbe("A", log);

public sealed class PublishProbeB(TraceLog log) : PublishProbe("B", log);

public sealed class PublishProbeC(TraceLog log) : PublishProbe("C", log);
