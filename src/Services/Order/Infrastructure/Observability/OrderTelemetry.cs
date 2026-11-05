using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Order.Infrastructure.Observability;

public static class OrderTelemetry
{
    public const string SourceName = "Order";

    public static readonly ActivitySource Source = new(SourceName);

    public static readonly Meter Meter = new(SourceName);
}
