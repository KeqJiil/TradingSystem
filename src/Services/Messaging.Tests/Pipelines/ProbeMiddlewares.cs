using Messaging.Abstractions;

namespace Messaging.Tests.Pipelines;

public sealed class TraceLog
{
    public List<string> Entries { get; } = new();
}

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

public class MutateMiddleware(string a) : IMessageDeliveryMiddleware
{
    public Task<ConsumeOutcome> OnDeliveryAsync<TMessage>(DeliveryContext<TMessage> context,
        DeliveryDelegate next, CancellationToken cancellationToken)
    {
        context.Headers[a] = "true";
        return next();
    }
}

public class FailureMiddleware : IMessageDeliveryMiddleware
{
    public Task<ConsumeOutcome> OnDeliveryAsync<TMessage>(DeliveryContext<TMessage> context,
        DeliveryDelegate next, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.DeadLetter));
    }
}

public class FailureMiddleware2 : IMessageDeliveryMiddleware
{
    public async Task<ConsumeOutcome> OnDeliveryAsync<TMessage>(DeliveryContext<TMessage> context,
        DeliveryDelegate next, CancellationToken cancellationToken)
    {
        await next();
        return new ConsumeOutcome(MessageConsumeResult.DeadLetter);
    }
}

public sealed class OutcomeLog
{
    public List<ConsumeOutcome> Seen { get; } = new();
}

public sealed class OutcomeCaptureMiddleware(OutcomeLog log) : IMessageDeliveryMiddleware
{
    public async Task<ConsumeOutcome> OnDeliveryAsync<TMessage>(DeliveryContext<TMessage> context,
        DeliveryDelegate next, CancellationToken cancellationToken)
    {
        var outcome = await next();
        log.Seen.Add(outcome);
        return outcome;
    }
}

public sealed class ProbeA(TraceLog log) : ProbeMiddleware("A", log);

public sealed class ProbeB(TraceLog log) : ProbeMiddleware("B", log);

public sealed class ProbeC(TraceLog log) : ProbeMiddleware("C", log);