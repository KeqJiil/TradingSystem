using Order.Domain.Aggregates;

namespace Order.Domain.Events;

public record NewOrderPendingEvent(
    Guid OrderId,
    Symbols Symbol,
    int Quantity,
    decimal? LimitPrice,
    TimeInForce TimeInForce,
    OrderSide Side,
    OrderType Type,
    DateTimeOffset CreatedAt) : DomainEvent;