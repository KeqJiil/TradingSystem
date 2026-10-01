using Messaging.Abstractions;
using Microsoft.Extensions.Logging;

namespace Messaging.Middlewares;

public class LoggingMiddleware(ILogger<LoggingMiddleware> logger)
    : IMessagePublishMiddleware, IMessageDeliveryMiddleware
{
    public async Task<ConsumeOutcome> OnDeliveryAsync(IDeliveryContext context, DeliveryDelegate next,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Delivering message with ID: {MessageId}, from topic {Topic}", context.MessageId,
            context.Topic);

        var outcome = await next();

        logger.LogDebug("Message with ID: {MessageId} from topic {Topic} consumed with {Outcome} {Reason}",
            context.MessageId, context.Topic, outcome.Kind, outcome.Reason);

        return outcome;
    }

    public Task OnPublishAsync(IPublishContext context, Func<Task> next, CancellationToken cancellationToken)
    {
        logger.LogDebug("Publishing message with ID: {MessageId}, to topic {Topic}", context.MessageId, context.Topic);
        return next();
    }
}