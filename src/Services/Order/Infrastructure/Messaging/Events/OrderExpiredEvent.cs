namespace Order.Infrastructure.Messaging.Events;

public record OrderExpiredEvent(Guid AggregateId, DateTimeOffset OccurredAt) : ExternalEvent<OrderExpiredEvent>, IExternalEvent;
