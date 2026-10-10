namespace Messaging.Abstractions;

public delegate Task<PublishOutcome> PublishDelegate(Dictionary<string, string> headers, CancellationToken cancellationToken);

public interface IMessagePublishMiddleware
{
    /// <summary>
    /// Middleware method that is called when a message is being published. This method allows for custom logic to be executed before the message is published.
    /// </summary>
    /// <param name="context">Message Context after Serialization</param>
    /// <param name="next">Function to invoke the next middleware in the pipeline</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The outcome of the publish operation</returns>
    PublishDelegate OnPublishAsync<TMessage>(PublishContext<TMessage> context, PublishDelegate next, CancellationToken cancellationToken);
}

public interface IMessagePublishMiddleware<TMessage>
{
    PublishDelegate OnPublishAsync(PublishContext<TMessage> context, PublishDelegate next, CancellationToken cancellationToken);
}

public delegate Task<ConsumeOutcome> DeliveryDelegate();

public interface IMessageDeliveryMiddleware
{
    /// <summary>
    /// Middleware method that is called when a message is being delivered. This method allows for custom logic to be executed before the message is delivered.
    /// </summary>
    /// <param name="context">Message Context from Serialization</param>
    /// <param name="next">Function to invoke the next middleware in the pipeline</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The outcome of the consume operation</returns>
    Task<ConsumeOutcome> OnDeliveryAsync<TMessage>(DeliveryContext<TMessage> context, DeliveryDelegate next, CancellationToken cancellationToken);
}

public interface IMessageDeliveryMiddleware<TMessage>
{
    Task<ConsumeOutcome> OnDeliveryAsync(DeliveryContext<TMessage> context, DeliveryDelegate next, CancellationToken cancellationToken);
}