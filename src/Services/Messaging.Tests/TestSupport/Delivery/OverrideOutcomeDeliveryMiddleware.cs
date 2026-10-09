namespace Messaging.Tests.TestSupport.Delivery;

public sealed class OverrideOutcomeDeliveryMiddleware : IMessageDeliveryMiddleware
{
    public async Task<ConsumeOutcome> OnDeliveryAsync<TMessage>(DeliveryContext<TMessage> context,
        DeliveryDelegate next, CancellationToken cancellationToken)
    {
        await next();
        return new ConsumeOutcome(MessageConsumeResult.DeadLetter);
    }
}
