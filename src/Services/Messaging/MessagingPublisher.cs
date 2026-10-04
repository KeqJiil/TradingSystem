using Confluent.Kafka;
using Messaging.Abstractions;

namespace Messaging;

internal class MessagingPublisher(IServiceProvider sp, IPublishTerminal publishTerminal) : IMessagePublisher
{
    public Task<PublishOutcome> PublishAsync<TMessage>(TMessage message, PublishOptions? options,
        CancellationToken cancellationToken)
    {
        if (options is null) return Task.FromResult(new PublishOutcome(false, "PublishOptions cannot be null"));

        var context = new PublishContext<TMessage>(
            options.Value.Topic, options.Value.KeyId, Guid.NewGuid().ToString(),
            typeof(TMessage).Name, message, new Dictionary<string, string>(options.Value.Headers));

        return MessagingPublishPipeline.RunPublish(context, sp,
            publishTerminal, cancellationToken);
    }
}