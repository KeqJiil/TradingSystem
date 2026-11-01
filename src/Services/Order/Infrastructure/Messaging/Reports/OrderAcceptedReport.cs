namespace Order.Infrastructure.Messaging.Reports;

public record OrderAcceptedReport(Guid OrderId, DateTimeOffset AcceptedAt);
