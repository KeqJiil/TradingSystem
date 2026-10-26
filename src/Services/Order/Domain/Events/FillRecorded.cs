using Order.Domain.VO;

namespace Order.Domain.Events;

public record FillRecorded(Guid OrderId, Guid ExecutionId, Quantity Quantity, Price Price, DateTimeOffset FilledAt)
    : DomainEvent(FilledAt);