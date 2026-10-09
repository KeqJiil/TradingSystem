namespace Messaging.Tests.TestSupport.Delivery;

public sealed class ScopedValue;

public sealed class ScopedValues
{
    public List<ScopedValue> Items { get; } = new();
}

public sealed class ScopedTouchMiddleware(ScopedValue value, ScopedValues seen) : IMessageDeliveryMiddleware
{
    public Task<ConsumeOutcome> OnDeliveryAsync<TMessage>(DeliveryContext<TMessage> context,
        DeliveryDelegate next, CancellationToken cancellationToken)
    {
        seen.Items.Add(value);
        return next();
    }
}
