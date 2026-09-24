using System.Diagnostics;

namespace Stock.Infrastructure.Observability;

public static class StockTelemetry
{
    public const string SourceName = "Stock";

    public static readonly ActivitySource Source = new(SourceName);
}
