namespace Order.Domain.Events;

public record OrderExpired(Guid OrderId, int FilledQuantity, int ExpiredQuantity, DateTimeOffset ExpiredAt) : DomainEvent(ExpiredAt);