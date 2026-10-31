namespace Order.Infrastructure.Messaging.Reports;

public record FillReport(Guid OrderId, Guid ExecutionId, int Quantity, decimal Price, DateTimeOffset FilledAt);
