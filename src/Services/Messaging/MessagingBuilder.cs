using Messaging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging;

public static class MessagingBuilderExtension
{
    public static void AddMessaging(this IServiceCollection builder, Action<MessagingBuilder> configure)
    {
        var registry = new MessagingRegistry();
        
        builder.AddSingleton(registry);
        
        var result = new MessagingBuilder(builder);
        configure(result);

        result.Messages.ForEach(c => registry.Messages.Add(c.MessageType.ToString(), (c.Topic, c.Serializer)));
        result.Consumers.ForEach(c =>
        {
            registry.Consumers.TryAdd((c.Options.Topic, c.Options.ConsumerGroup), new Dictionary<string, ConsumerBinding>());
            var serializer = registry.Messages[c.MessageType.ToString()].Serializer;
            registry.Consumers[(c.Options.Topic, c.Options.ConsumerGroup)].Add(c.MessageType.ToString(), new ConsumerBinding(c.Options, c.Bind.Invoke(serializer)));
        });
    }

    public static void BuildMessaging(this IServiceProvider sp)
    {
        var registry = sp.GetRequiredService<MessagingRegistry>();
        
        registry.DeliveryPipeline = (ctx, services, ct) =>
        {
            var deliveryMiddlewares = services.GetKeyedServices<IMessageDeliveryMiddleware>("MessagingDeliveryMiddleware").ToList();
            var dispatch = registry.Consumers[(ctx.Topic, ctx.ConsumerGroup)][ctx.MessageType].Dispatch;
            DeliveryDelegate next = () => dispatch(services, ctx.Message, ct);

            for (var i = deliveryMiddlewares.Count - 1; i >= 0; i--)
            {
                var middleware = deliveryMiddlewares[i];
                var inner = next;
                next = () => middleware.OnDeliveryAsync(ctx, inner, ct);
            }

            return next();
        };
        
        registry.PublishPipeline = (ctx, services, ct) =>
        {
            var publishMiddlewares = services.GetKeyedServices<IMessagePublishMiddleware>("MessagingPublishMiddleware").ToList();
            PublishDelegate next = () => Task.CompletedTask;

            for (var i = publishMiddlewares.Count - 1; i >= 0; i--)
            {
                var middleware = publishMiddlewares[i];
                var inner = next;
                next = () => middleware.OnPublishAsync(ctx, inner, ct);
            }

            return next();
        };
    }
}

public class MessagingBuilder(IServiceCollection sc) : IMessagingBuilder
{
    internal List<PendingConsumer> Consumers { get; } = new();
    internal List<(string Topic, Type MessageType, IMessageSerializer Serializer)> Messages { get; } = new();
    
    public IServiceCollection Services => sc;
    
    public IMessagingBuilder AddConsumer<TMessage, TConsumer>(ConsumerOptions options)
        where TConsumer : class, IMessageConsumer<TMessage>
    {
        Consumers.Add(new PendingConsumer(typeof(TMessage), options, serializer =>
            (sp, payload, ct) =>
            {
                var result = serializer.Deserialize<TMessage>(payload);
                return result.Success
                    ? sp.GetRequiredService<TConsumer>().ConsumeAsync(result.Message, ct)
                    : Task.FromResult(new ConsumeOutcome(MessageConsumeResult.DeadLetter, result.Error));
            }));
        sc.AddScoped<TConsumer>();
        return this;
    }

    public IMessagingBuilder AddMessage<TMessage>(string topic, IMessageSerializer serializer)
    {
        Messages.Add((topic, typeof(TMessage), serializer));
        return this;
    }

    public IMessagingBuilder AddDeliveryMiddleware<TMiddleware>() where TMiddleware : class, IMessageDeliveryMiddleware
    {
        sc.AddKeyedScoped<IMessageDeliveryMiddleware, TMiddleware>("MessagingDeliveryMiddleware");
        return this;
    }

    public IMessagingBuilder AddPublishMiddleware<TMiddleware>() where TMiddleware : class, IMessagePublishMiddleware
    {
        sc.AddKeyedScoped<IMessagePublishMiddleware, TMiddleware>("MessagingPublishMiddleware");
        return this;
    }
}

internal sealed record PendingConsumer(
    Type MessageType,
    ConsumerOptions Options,
    Func<IMessageSerializer, DispatchDelegate> Bind);
    
