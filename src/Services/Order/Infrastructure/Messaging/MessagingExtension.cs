using Messaging;
using Messaging.Abstractions;
using Messaging.EFCore.Inbox;
using Messaging.EFCore.Outbox;
using Messaging.Middlewares;
using Messaging.Serializers;
using Order.Infrastructure.Messaging.Consumers;
using Order.Infrastructure.Messaging.Reports;
using Order.Infrastructure.Messaging.Serialization;
using Order.Infrastructure.Messaging.Translators;
using Order.Infrastructure.Options;
using Order.Infrastructure.Persistence.Configuration;
using Order.Infrastructure.Topics;
using TradingSystem.Contracts.ProtobufClasses.Order;
using TradingSystem.Contracts.ProtobufClasses.Stock;

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
            cfg.AddOutbox<OrderDbContext>(o => o.AgeMetricInterval = kafka.OutboxAgeMetricInterval);
            cfg.AddInbox<OrderDbContext>();
            
            cfg.AddExceptionHandling();
            cfg.AddTracing();

            var orderEvents = OrderTopic.OrderEvents.Name();
            cfg.AddMessage<OrderPlacedEvent>(orderEvents, new ProtobufSerializer<OrderPlacedEvent>())
                .AddMessage<OrderCancelRequestedEvent>(orderEvents, new ProtobufSerializer<OrderCancelRequestedEvent>())
                .AddMessage<OrderCanceledEvent>(orderEvents, new ProtobufSerializer<OrderCanceledEvent>())
                .AddMessage<OrderFilledEvent>(orderEvents, new ProtobufSerializer<OrderFilledEvent>())
                .AddMessage<OrderExpiredEvent>(orderEvents, new ProtobufSerializer<OrderExpiredEvent>())
                .AddMessage<OrderRejectedEvent>(orderEvents, new ProtobufSerializer<OrderRejectedEvent>())
                .AddMessage<OrderApprovedEvent>(orderEvents, new ProtobufSerializer<OrderApprovedEvent>())
                .AddMessage<OrderCancelRejectedEvent>(orderEvents, new ProtobufSerializer<OrderCancelRejectedEvent>())
                .AddMessage<OrderFillRecordedEvent>(orderEvents, new ProtobufSerializer<OrderFillRecordedEvent>());

            var executionReports = MatcherTopic.ExecutionReports.Name();
            cfg.AddMessage<OrderAcceptedReport>(executionReports, serializer)
                .AddMessage<OrderRejectedReport>(executionReports, serializer)
                .AddMessage<FillReport>(executionReports, serializer)
                .AddMessage<OrderExpiredReport>(executionReports, serializer)
                .AddMessage<OrderCanceledReport>(executionReports, serializer)
                .AddMessage<CancelRejectedReport>(executionReports, serializer);

            var executionReportsConsumer = new ConsumerOptions(executionReports, "order-execution-reports-group")
                { MaxAttempts = kafka.ExecutionReportsMaxAttempts };
            cfg.AddConsumer<OrderAcceptedReport, OrderAcceptedConsumer>(executionReportsConsumer)
                .AddConsumer<OrderRejectedReport, OrderRejectedConsumer>(executionReportsConsumer)
                .AddConsumer<FillReport, FillConsumer>(executionReportsConsumer)
                .AddConsumer<OrderExpiredReport, OrderExpiredConsumer>(executionReportsConsumer)
                .AddConsumer<OrderCanceledReport, OrderCanceledConsumer>(executionReportsConsumer)
                .AddConsumer<CancelRejectedReport, CancelRejectedConsumer>(executionReportsConsumer);

            var stockCreated = StockTopic.StockCreated.Name();
            cfg.AddMessage<StockCreatedEvent>(stockCreated, new ProtobufSerializer<StockCreatedEvent>());
            cfg.AddConsumer<StockCreatedEvent, StockCreatedConsumer>(
                new ConsumerOptions(stockCreated, "order-stock-created-group")
                    { MaxAttempts = kafka.StockEventsMaxAttempts });

            var stockNameChanged = StockTopic.StockNameChanged.Name();
            cfg.AddMessage<NameChangedEvent>(stockNameChanged, new ProtobufSerializer<NameChangedEvent>());
            cfg.AddConsumer<NameChangedEvent, NameChangedConsumer>(
                new ConsumerOptions(stockNameChanged, "order-stock-name-changed-group")
                    { MaxAttempts = kafka.StockEventsMaxAttempts });

            var stockStatusToggled = StockTopic.StockStatusToggled.Name();
            cfg.AddMessage<StockToggledStatusEvent>(stockStatusToggled,
                new ProtobufSerializer<StockToggledStatusEvent>());
            cfg.AddConsumer<StockToggledStatusEvent, StockToggledStatusConsumer>(
                new ConsumerOptions(stockStatusToggled, "order-stock-status-toggled-group")
                    { MaxAttempts = kafka.StockEventsMaxAttempts });

            var stockTradingTimeChanged = StockTopic.StockTradingTimeChanged.Name();
            cfg.AddMessage<TimeChangedEvent>(stockTradingTimeChanged, new ProtobufSerializer<TimeChangedEvent>());
            cfg.AddConsumer<TimeChangedEvent, TimeChangedConsumer>(
                new ConsumerOptions(stockTradingTimeChanged, "order-stock-trading-time-changed-group")
                    { MaxAttempts = kafka.StockEventsMaxAttempts });
        }, options =>
        {
            options.BootstrapServers = kafka.BootstrapServers;
            options.ProducerClientId = kafka.ProducerClientId;
        });

        return services;
    }
}
