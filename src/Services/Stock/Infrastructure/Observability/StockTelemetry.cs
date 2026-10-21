using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Stock.Infrastructure.Observability;

public static class StockTelemetry
{
    public const string SourceName = "Stock";

    public static readonly ActivitySource Source = new(SourceName);

    public static readonly Meter Meter = new(SourceName);
}
