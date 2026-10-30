namespace Order.Infrastructure.Messaging.Events;

public record OrderCancelRequestedEvent(Guid AggregateId, DateTimeOffset OccurredAt) : ExternalEvent<OrderCancelRequestedEvent>, IExternalEvent;
