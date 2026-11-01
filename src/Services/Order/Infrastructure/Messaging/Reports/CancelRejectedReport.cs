namespace Order.Infrastructure.Messaging.Reports;

public record CancelRejectedReport(Guid OrderId, string Reason, DateTimeOffset RejectedAt);
