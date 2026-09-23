using OpenTelemetry;
using OpenTelemetry.Logs;
using Stock.Infrastructure;

namespace Stock.Presentation.Builder.Observability;

public class CorrelationLogProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord record)
    {
        if (!CorrelationContext.CorrelationId.HasValue) return;
        record.Attributes =
        [
            ..record.Attributes ?? [],
            new KeyValuePair<string, object?>("CorrelationId", CorrelationContext.CorrelationId.Value)
        ];
    }
}