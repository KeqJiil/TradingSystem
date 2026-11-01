namespace Order.Infrastructure.Messaging.Reports;

public record OrderRejectedReport(Guid OrderId, string Reason, DateTimeOffset RejectedAt);
