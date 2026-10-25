namespace Order.Domain.Events;

public record OrderRejected(Guid OrderId, string Reason, DateTimeOffset RejectedAt) : DomainEvent;