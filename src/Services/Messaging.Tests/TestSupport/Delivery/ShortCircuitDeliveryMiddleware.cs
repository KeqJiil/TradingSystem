namespace Messaging.Tests.TestSupport.Delivery;

public sealed class ShortCircuitDeliveryMiddleware : IMessageDeliveryMiddleware
{
    public Task<ConsumeOutcome> OnDeliveryAsync<TMessage>(DeliveryContext<TMessage> context,
        DeliveryDelegate next, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.DeadLetter));
    }
}
