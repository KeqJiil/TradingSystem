using Messaging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging;

public interface IMessagingBuilder
{
    public IServiceCollection Services { get; }
    
    IMessagingBuilder AddConsumer<TMessage, TConsumer>(ConsumerOptions options)
        where TConsumer : class, IMessageConsumer<TMessage>;

    IMessagingBuilder AddMessage<TMessage>(string topic, IMessageSerializer serializer);

    IMessagingBuilder AddDeliveryMiddleware<TMiddleware>() where TMiddleware : class, IMessageDeliveryMiddleware;

    IMessagingBuilder AddPublishMiddleware<TMiddleware>() where TMiddleware : class, IMessagePublishMiddleware;
}