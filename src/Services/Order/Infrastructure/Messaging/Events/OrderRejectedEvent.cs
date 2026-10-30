namespace Order.Infrastructure.Messaging.Events;

public record OrderRejectedEvent(Guid AggregateId, string Reason, DateTimeOffset OccurredAt) : IExternalEvent;
