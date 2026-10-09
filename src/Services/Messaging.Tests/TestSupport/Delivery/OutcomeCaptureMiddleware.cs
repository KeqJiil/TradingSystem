namespace Messaging.Tests.TestSupport.Delivery;

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
