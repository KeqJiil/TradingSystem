using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Messaging;

public static class MessagingTelemetry
{
    public const string SourceName = "Messaging";
    public const string MeterName = "Messaging";

    internal static readonly ActivitySource Source = new(SourceName);
    internal static readonly Meter Meter = new(MeterName);
}
