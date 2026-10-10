using Confluent.Kafka;
using Messaging.Abstractions;
using Messaging.Dlq;
using Messaging.Serializers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Messaging;

public static class MessagingBuilderExtension
{
    public static void AddMessaging(this IServiceCollection builder, Action<MessagingBuilder> configure,
        Action<MessagingOptions> configureOptions)
    {
        var registry = new MessagingRegistry();

        builder.AddSingleton<IExceptionToOutcome, BasicExceptionToOutcome>();
        builder.AddSingleton<IMessageSerializer, JsonDefaultSerializer>();

        builder.AddSingleton<IAdminClient>(sp =>
            new AdminClientBuilder(new AdminClientConfig
                    { BootstrapServers = sp.GetRequiredService<IOptions<MessagingOptions>>().Value.BootstrapServers })
                .Build());
        builder.AddSingleton<KafkaTopicRegister>();
        builder.AddHostedService<TopicRegistrationService>();

        builder.AddSingleton(registry);

        var result = new MessagingBuilder(builder);
        configure(result);

        var options = new MessagingOptions();
        configureOptions(options);
        options.Validate();

        builder.AddOptions<MessagingOptions>().Configure(o =>
        {
            o.UseAutoCommit = options.UseAutoCommit;
            o.AutoCommitIntervalMs = options.AutoCommitIntervalMs;
            o.ConnectionString = options.ConnectionString;
            o.ProducerIdempotency = options.ProducerIdempotency;
            o.Topics = options.Topics;
            o.BootstrapServers = options.BootstrapServers;
            o.ProducerClientId = options.ProducerClientId;
            o.EnableTopicRegistration = options.EnableTopicRegistration;
            o.MaxAttempts = options.MaxAttempts;
            o.RetryDelay = options.RetryDelay;
            o.DefaultNumPartitions = options.DefaultNumPartitions;
            o.DefaultReplicationFactor = options.DefaultReplicationFactor;
            o.MessageTimeoutMs = options.MessageTimeoutMs;
        });

        builder.AddSingleton<IPublishTerminal, DefaultPublishTerminal>();
        builder.TryAddScoped<IMessagePublisher, MessagingPublisher>();

        builder.AddSingleton<BasicKafkaProducer>();
        builder.AddSingleton<IDeadLetterPublisher, DlqKafkaProducer>();

        result.Messages.ForEach(c => registry.Messages.Add(c.MessageType, (c.Topic, c.Serializer)));
        result.Consumers.ForEach(c =>
        {
            if (!registry.Messages.TryGetValue(c.MessageType, out var message))
                throw new InvalidOperationException(
                    $"A consumer is registered for {c.MessageType.Name} but the message is not. Call AddMessage<{c.MessageType.Name}>(topic, serializer).");
            if (c.Options.MaxAttempts is < 1)
                throw new InvalidOperationException(
                    $"{nameof(ConsumerOptions.MaxAttempts)} of the consumer for {c.MessageType.Name} must be at least 1.");

            registry.Consumers.TryAdd((c.Options.Topic, c.Options.ConsumerGroup),
                new Dictionary<string, ConsumerBinding>());

            registry.Consumers[(c.Options.Topic, c.Options.ConsumerGroup)].Add(c.MessageType.Name,
                new ConsumerBinding(c.Options, c.Bind.Invoke(message.Serializer)));
        });

        foreach (var (topic, group) in registry.Consumers.Keys)
            builder.AddSingleton<IHostedService>(sp => new BasicKafkaConsumer(
                sp, registry, options,
                sp.GetRequiredService<ILogger<BasicKafkaConsumer>>(),
                sp.GetRequiredService<IDeadLetterPublisher>(),
                $"{options.ProducerClientId}-{group}-{topic}",
                group,
                topic));
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
                var result = serializer.Deserialize<TMessage>(payload.Payload);
                if (!result.Success)
                    return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.DeadLetter, result.Error));

                var context = new DeliveryContext<TMessage>(payload.Topic, payload.ConsumerGroup, payload.Partition,
                    payload.Offset,
                    payload.Headers.TryGetValue(MessagingHeaders.MessageId, out var messageId)
                        ? messageId
                        : string.Empty,
                    payload.MessageType,
                    result.Message, payload.Timestamp, payload.Headers
                );
                return MessagingPipeline.RunDelivery(context, sp,
                    () => sp.GetRequiredService<TConsumer>().ConsumeAsync(result.Message, ct), ct);
            }));
        sc.AddScoped<TConsumer>();
        return this;
    }

    public IMessagingBuilder AddProducer<TProducer>() where TProducer : class, IMessagePublisher
    {
        sc.AddScoped<IMessagePublisher, TProducer>();
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
    
    public IMessagingBuilder AddSerializer<TSerializer>() where TSerializer : class, IMessageSerializer
    {
        sc.AddScoped<IMessageSerializer, TSerializer>();
        return this;
    }
}

internal sealed record PendingConsumer(
    Type MessageType,
    ConsumerOptions Options,
    Func<IMessageSerializer, DispatchDelegate> Bind);