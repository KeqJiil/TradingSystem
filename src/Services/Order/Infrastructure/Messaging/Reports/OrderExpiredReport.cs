namespace Order.Infrastructure.Messaging.Reports;

public record OrderExpiredReport(Guid OrderId, DateTimeOffset ExpiredAt);
