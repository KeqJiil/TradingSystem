namespace Messaging.Tests.TestSupport.Delivery;

public abstract class TypedProbeMiddleware<TMessage>(string name, TraceLog log) : IMessageDeliveryMiddleware<TMessage>
{
    public async Task<ConsumeOutcome> OnDeliveryAsync(DeliveryContext<TMessage> context, DeliveryDelegate next,
        CancellationToken cancellationToken)
    {
        log.Entries.Add($"{name}:before");
        var outcome = await next();
        log.Entries.Add($"{name}:after");
        return outcome;
    }
}

public sealed class TypedProbeX(TraceLog log) : TypedProbeMiddleware<TestMessage>("X", log);

public sealed class TypedProbeY(TraceLog log) : TypedProbeMiddleware<TestMessage>("Y", log);

public sealed class TypedProbeOther(TraceLog log) : TypedProbeMiddleware<TestMessage2>("Other", log);
