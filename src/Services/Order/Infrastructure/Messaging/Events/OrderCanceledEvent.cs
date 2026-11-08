namespace Order.Infrastructure.Messaging.Events;

public record OrderCanceledEvent(Guid AggregateId, string Reason, int FilledQuantity, int CanceledQuantity, DateTimeOffset OccurredAt) : ExternalEvent<OrderCanceledEvent>, IExternalEvent;
