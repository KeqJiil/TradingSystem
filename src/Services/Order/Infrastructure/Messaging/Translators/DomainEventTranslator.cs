using System.Globalization;
using Google.Protobuf.WellKnownTypes;
using Order.Domain.Events;
using Order.Infrastructure.Messaging.Events;
using TradingSystem.Contracts.ProtobufClasses.Order;

namespace Order.Infrastructure.Messaging.Translators;

public class DomainEventTranslator : IDomainEventTranslator
{
    public ExternalEvent? Translate(DomainEvent domainEvent)
    {
        return domainEvent switch
        {
            OrderPlaced e => ExternalEvent.Create(e.OrderId, ToOrderPlacedEvent(e)),
            OrderCancelRequested e => ExternalEvent.Create(e.OrderId, new OrderCancelRequestedEvent
            {
                AggregateId = e.OrderId.ToString(),
                OccurredAt = Timestamp.FromDateTimeOffset(e.OccurredOn)
            }),
            OrderCanceled e => ExternalEvent.Create(e.OrderId, new OrderCanceledEvent
            {
                AggregateId = e.OrderId.ToString(),
                Reason = e.Reason,
                FilledQuantity = e.FilledQuantity,
                CanceledQuantity = e.CanceledQuantity,
                OccurredAt = Timestamp.FromDateTimeOffset(e.OccurredOn)
            }),
            OrderFilled e => ExternalEvent.Create(e.OrderId, new OrderFilledEvent
            {
                AggregateId = e.OrderId.ToString(),
                Quantity = e.Quantity,
                OccurredAt = Timestamp.FromDateTimeOffset(e.OccurredOn)
            }),
            OrderExpired e => ExternalEvent.Create(e.OrderId, new OrderExpiredEvent
            {
                AggregateId = e.OrderId.ToString(),
                FilledQuantity = e.FilledQuantity,
                ExpiredQuantity = e.ExpiredQuantity,
                OccurredAt = Timestamp.FromDateTimeOffset(e.OccurredOn)
            }),
            OrderRejected e => ExternalEvent.Create(e.OrderId, new OrderRejectedEvent
            {
                AggregateId = e.OrderId.ToString(),
                Reason = e.Reason,
                OccurredAt = Timestamp.FromDateTimeOffset(e.OccurredOn)
            }),
            OrderApproved e => ExternalEvent.Create(e.OrderId, new OrderApprovedEvent
            {
                AggregateId = e.OrderId.ToString(),
                OccurredAt = Timestamp.FromDateTimeOffset(e.OccurredOn)
            }),
            OrderCancelRejected e => ExternalEvent.Create(e.OrderId, new OrderCancelRejectedEvent
            {
                AggregateId = e.OrderId.ToString(),
                Reason = e.Reason,
                OccurredAt = Timestamp.FromDateTimeOffset(e.OccurredOn)
            }),
            FillRecorded e => ExternalEvent.Create(e.OrderId, new OrderFillRecordedEvent
            {
                AggregateId = e.OrderId.ToString(),
                ExecutionId = e.ExecutionId.ToString(),
                Quantity = e.Quantity.Value,
                Price = e.Price.Value.ToString(CultureInfo.InvariantCulture),
                OccurredAt = Timestamp.FromDateTimeOffset(e.OccurredOn)
            }),
            _ => null
        };
    }

    private static OrderPlacedEvent ToOrderPlacedEvent(OrderPlaced e)
    {
        var message = new OrderPlacedEvent
        {
            AggregateId = e.OrderId.ToString(),
            UserId = e.UserId.ToString(),
            Symbol = e.Symbol.ToString(),
            Side = e.Side.ToString(),
            Quantity = e.Quantity.Value,
            TimeInForce = e.OrderTerms.TimeInForce.ToString(),
            Type = e.OrderTerms.Type.ToString(),
            OccurredAt = Timestamp.FromDateTimeOffset(e.OccurredOn)
        };

        if (e.OrderTerms.LimitPrice is not null)
            message.LimitPrice = e.OrderTerms.LimitPrice.Value.ToString(CultureInfo.InvariantCulture);

        return message;
    }
}
