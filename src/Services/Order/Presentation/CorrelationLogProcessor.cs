using Messaging.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Order.Presentation;

public class CorrelationLogProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord record)
    {
        if (MessagingCorrelation.CorrelationId is not { } correlationId) return;
        record.Attributes =
        [
            ..record.Attributes ?? [],
            new KeyValuePair<string, object?>("CorrelationId", correlationId)
        ];
    }
}
