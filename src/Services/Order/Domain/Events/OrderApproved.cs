namespace Order.Domain.Events;

public record OrderApproved(Guid OrderId, DateTimeOffset ApprovedAt) : DomainEvent(ApprovedAt);
