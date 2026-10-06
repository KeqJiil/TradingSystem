using System.Diagnostics;

namespace Messaging;

public static class MessagingTelemetry
{
    public const string SourceName = "Messaging";

    internal static readonly ActivitySource Source = new(SourceName);
}
