namespace Order.Infrastructure.Messaging.Events;

public record OrderExpiredEvent(Guid AggregateId, int FilledQuantity, int ExpiredQuantity, DateTimeOffset OccurredAt) : ExternalEvent<OrderExpiredEvent>, IExternalEvent;
