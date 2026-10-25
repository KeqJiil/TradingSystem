namespace Order.Domain.Events;

public record OrderFilled(Guid OrderId, decimal Quantity, DateTimeOffset FilledAt)
    : DomainEvent(FilledAt);