using Messaging.Abstractions;
using Messaging.Tests.Pipelines;

namespace Messaging.Tests.TestSupport;

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

public sealed class ShortCircuitPublishMiddleware : IMessagePublishMiddleware
{
    public const string Error = "blocked";

    public PublishDelegate OnPublishAsync<TMessage>(PublishContext<TMessage> context, PublishDelegate next,
        CancellationToken cancellationToken)
    {
        return (_, _) => Task.FromResult(new PublishOutcome(false, Error));
    }
}

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
