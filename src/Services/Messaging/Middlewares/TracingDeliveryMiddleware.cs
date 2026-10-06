using System.Diagnostics;
using Messaging.Abstractions;

namespace Messaging.Middlewares;

public class TracingDeliveryMiddleware : IMessageDeliveryMiddleware
{
    public async Task<ConsumeOutcome> OnDeliveryAsync<TMessage>(DeliveryContext<TMessage> context,
        DeliveryDelegate next, CancellationToken cancellationToken)
    {
        context.Headers.TryGetValue(MessagingHeaders.TraceParent, out var traceParent);
        context.Headers.TryGetValue(MessagingHeaders.TraceState, out var traceState);
        var parent = ActivityContext.TryParse(traceParent, traceState, true, out var parsed) ? parsed : default;

        var correlationId = context.Headers.TryGetValue(MessagingHeaders.CorrelationId, out var header)
                            && !string.IsNullOrWhiteSpace(header)
            ? header
            : Guid.NewGuid().ToString();
        MessagingCorrelation.CorrelationId = correlationId;

        using var activity = MessagingTelemetry.Source.StartActivity($"process {context.Topic}",
            ActivityKind.Consumer, parent);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination.name", context.Topic);
        activity?.SetTag("messaging.consumer.group.name", context.ConsumerGroup);
        activity?.SetTag("messaging.destination.partition.id", context.Partition.ToString());
        activity?.SetTag("messaging.kafka.offset", context.Offset);
        activity?.SetTag("messaging.message.id", context.MessageId);
        activity?.SetTag("messaging.message.type", context.MessageType);
        activity?.SetTag("correlation.id", correlationId);

        try
        {
            var outcome = await next();
            activity?.SetTag("messaging.consume.outcome", outcome.Kind.ToString());
            if (outcome.Kind != MessageConsumeResult.Success)
                activity?.SetStatus(ActivityStatusCode.Error, outcome.Reason);
            return outcome;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
