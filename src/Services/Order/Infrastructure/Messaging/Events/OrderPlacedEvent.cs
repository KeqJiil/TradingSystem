namespace Order.Infrastructure.Messaging.Events;

public record OrderPlacedEvent(
    Guid AggregateId,
    Guid UserId,
    string Symbol,
    string Side,
    int Quantity,
    string TimeInForce,
    string Type,
    decimal? LimitPrice,
    DateTimeOffset OccurredAt
) : ExternalEvent<OrderPlacedEvent>, IExternalEvent;
