using System.Diagnostics;
using Stock.Infrastructure.Persistence;

namespace Stock.Infrastructure.Observability;

public static class OutboxTelemetry
{
    public static Activity? StartDispatch(OutboxData data, bool startNewTrace)
    {
        var stored = data.TraceParent is not null
                     && ActivityContext.TryParse(data.TraceParent, data.TraceState, out var context)
            ? context
            : default;

        var name = $"outbox.dispatch {data.EventType}";

        var activity = startNewTrace
            ? StockTelemetry.Source.StartActivity(name, ActivityKind.Internal, parentContext: default,
                links: stored == default ? null : [new ActivityLink(stored)])
            : StockTelemetry.Source.StartActivity(name, ActivityKind.Internal, stored);

        activity?.SetTag("outbox.id", data.Id.ToString());
        activity?.SetTag("outbox.event_type", data.EventType);
        activity?.SetTag("outbox.attempt", data.RetryCount);
        activity?.SetTag("outbox.wait_ms", (DateTimeOffset.UtcNow - data.CreatedAt).TotalMilliseconds);
        activity?.SetTag("correlation.id", data.CorrelationId?.ToString());
        return activity;
    }
}
