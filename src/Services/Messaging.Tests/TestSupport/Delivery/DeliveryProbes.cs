namespace Messaging.Tests.TestSupport.Delivery;

public abstract class ProbeMiddleware(string name, TraceLog log) : IMessageDeliveryMiddleware
{
    public async Task<ConsumeOutcome> OnDeliveryAsync<TMessage>(DeliveryContext<TMessage> context,
        DeliveryDelegate next, CancellationToken cancellationToken)
    {
        log.Entries.Add($"{name}:before");
        var outcome = await next();
        log.Entries.Add($"{name}:after");
        return outcome;
    }
}

public sealed class ProbeA(TraceLog log) : ProbeMiddleware("A", log);

public sealed class ProbeB(TraceLog log) : ProbeMiddleware("B", log);

public sealed class ProbeC(TraceLog log) : ProbeMiddleware("C", log);
