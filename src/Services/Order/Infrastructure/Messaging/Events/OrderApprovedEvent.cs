namespace Order.Infrastructure.Messaging.Events;

public record OrderApprovedEvent(Guid AggregateId, DateTimeOffset OccurredAt) : ExternalEvent<OrderApprovedEvent>, IExternalEvent;
