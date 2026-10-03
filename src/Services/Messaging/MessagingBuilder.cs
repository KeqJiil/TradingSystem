using Messaging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Messaging;

public static class MessagingBuilderExtension
{
    public static void AddMessaging(this IServiceCollection builder, Action<MessagingBuilder> configure,
        Action<MessagingOptions> configureOptions)
    {
        var registry = new MessagingRegistry();

        builder.AddSingleton(registry);

        var result = new MessagingBuilder(builder);
        configure(result);

        var options = new MessagingOptions();
        configureOptions(options);
        
        builder.AddSingleton(options);
        foreach (var (topic, group) in registry.Consumers.Keys)
        {
            builder.AddSingleton<IHostedService>(sp => new BasicKafkaConsumer(
                sp, registry, options,
                sp.GetRequiredService<ILogger<BasicKafkaConsumer>>(),
                clientId: $"{options.ProducerClientId}-{group}-{topic}",
                groupId: group,
                topic: topic));
        }
        

        builder.AddSingleton<BasicKafkaProducer>();

        result.Messages.ForEach(c => registry.Messages.Add(c.MessageType.ToString(), (c.Topic, c.Serializer)));
        result.Consumers.ForEach(c =>
        {
            registry.Consumers.TryAdd((c.Options.Topic, c.Options.ConsumerGroup),
                new Dictionary<string, ConsumerBinding>());
            var serializer = registry.Messages[c.MessageType.ToString()].Serializer;
            registry.Consumers[(c.Options.Topic, c.Options.ConsumerGroup)].Add(c.MessageType.ToString(),
                new ConsumerBinding(c.Options, c.Bind.Invoke(serializer)));
        });
    }
}

public class MessagingBuilder(IServiceCollection sc) : IMessagingBuilder
{
    internal List<PendingConsumer> Consumers { get; } = new();
    internal List<(string Topic, Type MessageType, IMessageSerializer Serializer)> Messages { get; } = new();

    public IServiceCollection Services => sc;
    public bool RetryTopic { get; set; } = true;
    public bool DeadLetterTopic { get; set; } = true;

    public IMessagingBuilder AddConsumer<TMessage, TConsumer>(ConsumerOptions options)
        where TConsumer : class, IMessageConsumer<TMessage>
    {
        Consumers.Add(new PendingConsumer(typeof(TMessage), options, serializer =>
            (sp, payload, ct) =>
            {
                var result = serializer.Deserialize<TMessage>(payload.Payload);
                if (!result.Success)
                    return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.DeadLetter, result.Error));

                var context = new DeliveryContext<TMessage>(payload.Topic, payload.ConsumerGroup, payload.Partition,
                    payload.Offset, payload.Headers.TryGetValue(MessagingHeaders.MessageId, out var messageId) ? messageId : string.Empty,
                    payload.MessageType,
                    result.Message, payload.Timestamp, payload.Headers
                );
                return MessagingPipeline.RunDelivery(context, sp,
                    () => sp.GetRequiredService<TConsumer>().ConsumeAsync(result.Message, ct), ct);
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