namespace Order.Domain.Events;

public record OrderCancelRejected(Guid OrderId, string Reason, DateTimeOffset RejectedAt) : DomainEvent(RejectedAt);