namespace Order.Domain.Events;

public record OrderFilled(Guid OrderId, int Quantity, DateTimeOffset FilledAt)
    : DomainEvent(FilledAt);