namespace Messaging.Tests.TestSupport.Delivery;

public sealed class MutateMiddleware(string a) : IMessageDeliveryMiddleware
{
    public Task<ConsumeOutcome> OnDeliveryAsync<TMessage>(DeliveryContext<TMessage> context,
        DeliveryDelegate next, CancellationToken cancellationToken)
    {
        context.Headers[a] = "true";
        return next();
    }
}
