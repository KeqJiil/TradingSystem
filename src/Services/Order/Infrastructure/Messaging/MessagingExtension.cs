using Messaging;
using Messaging.Abstractions;
using Messaging.EFCore.Inbox;
using Messaging.EFCore.Outbox;
using Messaging.Middlewares;
using Messaging.Serializers;
using Order.Infrastructure.Messaging.Consumers;
using Order.Infrastructure.Messaging.Events;
using Order.Infrastructure.Messaging.Reports;
using Order.Infrastructure.Messaging.Translators;
using Order.Infrastructure.Options;
using Order.Infrastructure.Persistence.Configuration;
using Order.Infrastructure.Topics;

namespace Order.Infrastructure.Messaging;

public static class MessagingExtension
{
    public static IServiceCollection MapMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var kafka = configuration.GetSection(KafkaOptions.Name).Get<KafkaOptions>()
                    ?? throw new InvalidOperationException($"{KafkaOptions.Name} is not configured.");

        var serializer = new JsonDefaultSerializer();

        services.AddSingleton<IDomainEventTranslator, DomainEventTranslator>();
        services.AddMessaging(cfg =>
        {
            cfg.AddOutbox<OrderDbContext>();
            cfg.AddInbox<OrderDbContext>();
            
            cfg.AddExceptionHandling();
            cfg.AddTracing();

            cfg.AddMessage<OrderPlacedEvent>(OrderTopic.OrderPlaced.Name(), serializer)
                .AddMessage<OrderCancelRequestedEvent>(OrderTopic.OrderCancelRequested.Name(), serializer)
                .AddMessage<OrderCanceledEvent>(OrderTopic.OrderCanceled.Name(), serializer)
                .AddMessage<OrderFilledEvent>(OrderTopic.OrderFilled.Name(), serializer)
                .AddMessage<OrderExpiredEvent>(OrderTopic.OrderExpired.Name(), serializer)
                .AddMessage<OrderRejectedEvent>(OrderTopic.OrderRejected.Name(), serializer);

            cfg.AddMessage<OrderAcceptedReport>(MatcherTopic.OrderAccepted.Name(), serializer)
                .AddMessage<OrderRejectedReport>(MatcherTopic.OrderRejected.Name(), serializer)
                .AddMessage<FillReport>(MatcherTopic.Fill.Name(), serializer)
                .AddMessage<OrderExpiredReport>(MatcherTopic.OrderExpired.Name(), serializer)
                .AddMessage<OrderCanceledReport>(MatcherTopic.OrderCanceled.Name(), serializer)
                .AddMessage<CancelRejectedReport>(MatcherTopic.CancelRejected.Name(), serializer);

            cfg.AddConsumer<OrderAcceptedReport, OrderAcceptedConsumer>(
                    new ConsumerOptions(MatcherTopic.OrderAccepted.Name(), "order-accepted-group"))
                .AddConsumer<OrderRejectedReport, OrderRejectedConsumer>(
                    new ConsumerOptions(MatcherTopic.OrderRejected.Name(), "order-rejected-group"))
                .AddConsumer<FillReport, FillConsumer>(
                    new ConsumerOptions(MatcherTopic.Fill.Name(), "order-fill-group"))
                .AddConsumer<OrderExpiredReport, OrderExpiredConsumer>(
                    new ConsumerOptions(MatcherTopic.OrderExpired.Name(), "order-expired-group"))
                .AddConsumer<OrderCanceledReport, OrderCanceledConsumer>(
                    new ConsumerOptions(MatcherTopic.OrderCanceled.Name(), "order-canceled-group"))
                .AddConsumer<CancelRejectedReport, CancelRejectedConsumer>(
                    new ConsumerOptions(MatcherTopic.CancelRejected.Name(), "order-cancel-rejected-group"));
        }, options =>
        {
            options.BootstrapServers = kafka.BootstrapServers;
            options.ProducerClientId = kafka.ProducerClientId;
        });

        return services;
    }
}
