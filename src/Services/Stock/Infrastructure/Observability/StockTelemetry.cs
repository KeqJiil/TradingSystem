using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Stock.Infrastructure.Observability;

public static class StockTelemetry
{
    public const string SourceName = "Stock";

    public static readonly ActivitySource Source = new(SourceName);

    public static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> DlqPublished = Meter.CreateCounter<long>(
        "stock.dlq.published", unit: "{message}", description: "Messages published to dead letter topics");
}
