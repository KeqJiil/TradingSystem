namespace Order.Infrastructure.Messaging.Events;

public record OrderCancelRejectedEvent(Guid AggregateId, string Reason, DateTimeOffset OccurredAt) : ExternalEvent<OrderCancelRejectedEvent>, IExternalEvent;
