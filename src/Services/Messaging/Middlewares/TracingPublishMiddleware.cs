using System.Diagnostics;
using Messaging.Abstractions;

namespace Messaging.Middlewares;

public class TracingPublishMiddleware : IMessagePublishMiddleware
{
    public PublishDelegate OnPublishAsync<TMessage>(PublishContext<TMessage> context, PublishDelegate next,
        CancellationToken cancellationToken)
    {
        return async (headers, token) =>
        {
            using var activity =
                MessagingTelemetry.Source.StartActivity($"send {context.Topic}", ActivityKind.Producer);
            activity?.SetTag("messaging.system", "kafka");
            activity?.SetTag("messaging.destination.name", context.Topic);
            activity?.SetTag("messaging.kafka.message.key", context.KeyId);
            activity?.SetTag("messaging.message.id", context.MessageId);
            activity?.SetTag("messaging.message.type", context.MessageType);

            if (Activity.Current is { Id: { } traceParent } current)
            {
                headers[MessagingHeaders.TraceParent] = traceParent;
                if (current.TraceStateString is { } traceState)
                    headers[MessagingHeaders.TraceState] = traceState;
            }

            if (MessagingCorrelation.CorrelationId is { } correlationId)
                headers.TryAdd(MessagingHeaders.CorrelationId, correlationId);

            try
            {
                var outcome = await next(headers, token);
                if (!outcome.IsSuccessful)
                    activity?.SetStatus(ActivityStatusCode.Error, outcome.ErrorMessage);
                return outcome;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);
                throw;
            }
        };
    }
}
