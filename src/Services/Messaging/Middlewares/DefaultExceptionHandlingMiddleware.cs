using Messaging.Abstractions;
using Microsoft.Extensions.Logging;

namespace Messaging.Middlewares;

public class DefaultExceptionHandlingMiddleware(
    ILogger<DefaultExceptionHandlingMiddleware> logger,
    IExceptionToOutcome exceptionToOutcome) : IMessageDeliveryMiddleware
{
    public async Task<ConsumeOutcome> OnDeliveryAsync<TMessage>(DeliveryContext<TMessage> context,
        DeliveryDelegate next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while processing the message of type {MessageType}",
                typeof(TMessage).Name);
            return exceptionToOutcome.Map(ex);
        }
    }
}