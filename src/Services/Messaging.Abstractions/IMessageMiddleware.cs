namespace Messaging.Abstractions;

public interface IMessagePublishMiddleware
{
    /// <summary>
    /// Middleware method that is called when a message is being published. This method allows for custom logic to be executed before the message is published.
    /// </summary>
    /// <param name="context">Message Context after Serialization</param>
    /// <param name="next">Function to invoke the next middleware in the pipeline</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    Task OnPublishAsync(IMessageContext context, Func<Task> next, CancellationToken cancellationToken);
}

public interface IMessageDeliveryMiddleware
{
    /// <summary>
    /// Middleware method that is called when a message is being delivered. This method allows for custom logic to be executed before the message is delivered.
    /// </summary>
    /// <param name="context">Message Context from Serialization</param>
    /// <param name="next">Function to invoke the next middleware in the pipeline</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    Task OnDeliveryAsync(IMessageContext context, Func<Task> next, CancellationToken cancellationToken);
}