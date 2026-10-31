namespace Order.Infrastructure.Messaging.Reports;

public record OrderCanceledReport(Guid OrderId, string Reason, DateTimeOffset CanceledAt);
