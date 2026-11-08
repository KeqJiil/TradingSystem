namespace Order.Domain.Events;

public record OrderCanceled(Guid OrderId, string Reason, int FilledQuantity, int CanceledQuantity, DateTimeOffset CancelledAt) : DomainEvent(CancelledAt);