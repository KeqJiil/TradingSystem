namespace Order.Infrastructure.Messaging.Events;

public record OrderCanceledEvent(Guid AggregateId, string Reason, DateTimeOffset OccurredAt) : ExternalEvent<OrderCanceledEvent>, IExternalEvent;
