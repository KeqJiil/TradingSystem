namespace Order.Domain.Events;

public record OrderApproved(Guid OrderId) : DomainEvent;