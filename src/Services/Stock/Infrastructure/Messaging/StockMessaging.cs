using Messaging;
using Messaging.Abstractions;
using Messaging.Dapper.Outbox;
using Messaging.Middlewares;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Consumers;
using Stock.Infrastructure.Options;
using Stock.Infrastructure.Serialization;
using Stock.Infrastructure.Topics;
using Proto = TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Stock.Infrastructure.Messaging;

public static class StockMessaging
{
    private const int ReadModelMaxAttempts = 15;

    public static void AddStockMessaging(this WebApplicationBuilder builder,
        Action<IMessagingBuilder>? configure = null)
    {
        var kafka = builder.Configuration.GetSection(KafkaOptions.Name).Get<KafkaOptions>()
                    ?? throw new InvalidOperationException($"{KafkaOptions.Name} is not configured.");

        builder.Services.AddMessaging(b =>
        {
            b.AddOutbox();
            b.AddExceptionHandling();
            b.AddTracing();
            b.Services.AddSingleton<IExceptionToOutcome, StockExceptionToOutcome>();

            b.AddMessage<Application.Events.StockCreatedEvent>(StockTopic.StockCreated.Name(),
                new ExternalEventSerializer<Application.Events.StockCreatedEvent, Proto.StockCreatedEvent>(
                    StockCreatedEventMapper.MapToExternal, StockCreatedEventMapper.MapFrom));
            b.AddMessage<Application.Events.StockToggledStatusEvent>(StockTopic.StockStatusToggled.Name(),
                new ExternalEventSerializer<Application.Events.StockToggledStatusEvent,
                    Proto.StockToggledStatusEvent>(
                    StockToggledStatusEventMapper.MapToExternal, StockToggledStatusEventMapper.MapFrom));
            b.AddMessage<Application.Events.PriceChangedEvent>(StockTopic.Price.Name(),
                new ExternalEventSerializer<Application.Events.PriceChangedEvent, Proto.PriceChangedEvent>(
                    PriceChangedEventMapper.MapToExternal, PriceChangedEventMapper.MapFrom));
            b.AddMessage<Application.Events.NameChangedEvent>(StockTopic.StockNameChanged.Name(),
                new ExternalEventSerializer<Application.Events.NameChangedEvent, Proto.NameChangedEvent>(
                    NameChangedEventMapper.MapToExternal, NameChangedEventMapper.MapFrom));
            b.AddMessage<Application.Events.TimeChangedEvent>(StockTopic.StockTradingTimeChanged.Name(),
                new ExternalEventSerializer<Application.Events.TimeChangedEvent, Proto.TimeChangedEvent>(
                    TimeChangedEventMapper.MapToExternal, TimeChangedEventMapper.MapFrom));
            b.AddMessage<Proto.PriceChangeRequestedEvent>(StockTopic.PriceChangeRequested.Name(),
                new ExternalEventSerializer<Proto.PriceChangeRequestedEvent, Proto.PriceChangeRequestedEvent>(e => e, e => e));

            b.AddConsumer<Application.Events.StockCreatedEvent, StockCreatedConsumer>(
                new ConsumerOptions(StockTopic.StockCreated.Name(), "stock-created-events-group"));
            b.AddConsumer<Application.Events.StockToggledStatusEvent, StockToggleStatusEventConsumer>(
                new ConsumerOptions(StockTopic.StockStatusToggled.Name(), "stock-toggle-events-group")
                    { MaxAttempts = ReadModelMaxAttempts });
            b.AddConsumer<Application.Events.PriceChangedEvent, PriceChangedConsumer>(
                new ConsumerOptions(StockTopic.Price.Name(), "price-change-events-group"));
            b.AddConsumer<Proto.PriceChangeRequestedEvent, PriceChangeRequestedConsumer>(
                new ConsumerOptions(StockTopic.PriceChangeRequested.Name(), "price-change-requested-events-group"));
            b.AddConsumer<Application.Events.NameChangedEvent, NameChangedConsumer>(
                new ConsumerOptions(StockTopic.StockNameChanged.Name(), "name-change-events-group")
                    { MaxAttempts = ReadModelMaxAttempts });
            b.AddConsumer<Application.Events.TimeChangedEvent, TimeChangedConsumer>(
                new ConsumerOptions(StockTopic.StockTradingTimeChanged.Name(), "time-change-events-group")
                    { MaxAttempts = ReadModelMaxAttempts });

            configure?.Invoke(b);
        }, o =>
        {
            o.BootstrapServers = kafka.BootstrapServers;
            o.ProducerClientId = kafka.ProducerClientId;
        });
    }
}
