using Messaging.Abstractions;

namespace Messaging;

internal class MessagingPublisher(IServiceProvider sp, IPublishTerminal publishTerminal, MessagingRegistry registry)
    : IMessagePublisher
{
    public Task<PublishOutcome> PublishAsync<TMessage>(TMessage message, PublishOptions options,
        CancellationToken cancellationToken)
    {
        if (!registry.Messages.TryGetValue(typeof(TMessage), out var registration))
            throw new InvalidOperationException(
                $"Message type {typeof(TMessage).Name} is not registered. Call AddMessage<{typeof(TMessage).Name}> first.");

        var context = new PublishContext<TMessage>(
            registration.Topic, options.KeyId, Guid.NewGuid().ToString(),
            typeof(TMessage).Name, message, new Dictionary<string, string>(options.Headers ?? []));

        return MessagingPublishPipeline.RunPublish(context, sp,
            publishTerminal, cancellationToken);
    }
}