namespace Order.Domain.Events;

public record OrderCancelRequested(Guid OrderId, DateTimeOffset RequestedAt) : DomainEvent(RequestedAt);