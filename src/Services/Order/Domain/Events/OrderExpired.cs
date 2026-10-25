namespace Order.Domain.Events;

public record OrderExpired(Guid OrderId, DateTimeOffset ExpiredAt) : DomainEvent(ExpiredAt);