using Order.Domain.Events;
using Order.Infrastructure.Messaging.Events;

namespace Order.Infrastructure.Messaging.Translators;

public class DomainEventTranslator : IDomainEventTranslator
{
    public IExternalEvent? Translate(DomainEvent domainEvent)
    {
        return domainEvent switch
        {
            OrderPlaced e => new OrderPlacedEvent(
                e.OrderId,
                e.UserId,
                e.Symbol.ToString(),
                e.Side.ToString(),
                e.Quantity.Value,
                e.OrderTerms.TimeInForce.ToString(),
                e.OrderTerms.Type.ToString(),
                e.OrderTerms.LimitPrice?.Value,
                e.OccurredOn),
            OrderCancelRequested e => new OrderCancelRequestedEvent(e.OrderId, e.OccurredOn),
            OrderCanceled e => new OrderCanceledEvent(e.OrderId, e.Reason, e.OccurredOn),
            OrderFilled e => new OrderFilledEvent(e.OrderId, e.Quantity, e.OccurredOn),
            OrderExpired e => new OrderExpiredEvent(e.OrderId, e.OccurredOn),
            OrderRejected e => new OrderRejectedEvent(e.OrderId, e.Reason, e.OccurredOn),
            _ => null
        };
    }
}
