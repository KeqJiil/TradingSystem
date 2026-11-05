namespace Order.Infrastructure.Messaging.Events;

public record OrderFillRecordedEvent(Guid AggregateId, Guid ExecutionId, int Quantity, decimal Price, DateTimeOffset OccurredAt) : ExternalEvent<OrderFillRecordedEvent>, IExternalEvent;
