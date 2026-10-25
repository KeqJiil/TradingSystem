namespace Order.Domain.Events;

public record OrderCanceled(Guid OrderId, string Reason, DateTimeOffset CancelledAt) : DomainEvent(CancelledAt);