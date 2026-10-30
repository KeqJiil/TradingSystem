namespace Order.Infrastructure.Messaging.Events;

public record OrderFilledEvent(Guid AggregateId, int Quantity, DateTimeOffset OccurredAt) : IExternalEvent;
