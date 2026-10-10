namespace Messaging.Tests.TestSupport.Publish;

public abstract class TypedPublishProbe<TMessage>(string name, TraceLog log) : IMessagePublishMiddleware<TMessage>
{
    public PublishDelegate OnPublishAsync(PublishContext<TMessage> context, PublishDelegate next,
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

public sealed class TypedPublishProbeX(TraceLog log) : TypedPublishProbe<SampleMessage>("X", log);

public sealed class TypedPublishProbeY(TraceLog log) : TypedPublishProbe<SampleMessage>("Y", log);

public sealed class TypedPublishProbeOther(TraceLog log) : TypedPublishProbe<TestMessage>("Other", log);
