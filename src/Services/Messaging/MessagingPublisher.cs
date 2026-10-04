using Messaging.Abstractions;

namespace Messaging;

internal class MessagingPublisher(IServiceProvider sp, IPublishTerminal publishTerminal, MessagingRegistry registry)
    : IMessagePublisher
{
    public Task<PublishOutcome> PublishAsync<TMessage>(TMessage message, PublishOptions? options,
        CancellationToken cancellationToken)
    {
        if (!registry.Messages.ContainsKey(typeof(TMessage)))
            throw new InvalidOperationException(
                $"Message type {typeof(TMessage).Name} is not registered. Call AddMessage<{typeof(TMessage).Name}> first.");

        if (options is null) return Task.FromResult(new PublishOutcome(false, "PublishOptions cannot be null"));

        var context = new PublishContext<TMessage>(
            options.Value.Topic, options.Value.KeyId, Guid.NewGuid().ToString(),
            typeof(TMessage).Name, message, new Dictionary<string, string>(options.Value.Headers));

        return MessagingPublishPipeline.RunPublish(context, sp,
            publishTerminal, cancellationToken);
    }
}