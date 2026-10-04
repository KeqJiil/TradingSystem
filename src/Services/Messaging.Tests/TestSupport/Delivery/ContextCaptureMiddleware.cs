namespace Messaging.Tests.TestSupport.Delivery;

public sealed class CapturedContext
{
    public object? Last { get; set; }
}

public sealed class ContextCaptureMiddleware(CapturedContext captured) : IMessageDeliveryMiddleware
{
    public Task<ConsumeOutcome> OnDeliveryAsync<TMessage>(DeliveryContext<TMessage> context,
        DeliveryDelegate next, CancellationToken cancellationToken)
    {
        captured.Last = context;
        return next();
    }
}
