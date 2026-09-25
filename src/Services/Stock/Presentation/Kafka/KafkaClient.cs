using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Stock.Application.Commands.ChangePrice;
using Stock.Application.Commands.CreateReadModel;
using Stock.Application.Commands.ReplayReadModel;
using Stock.Application.Commands.ToggleStatusReadModel;
using Stock.Application.Commands.UpdateNameReadModel;
using Stock.Application.Commands.UpdateTradingTimeReadModel;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Messaging.Consumers;
using Stock.Infrastructure.Messaging.Consumers.Dlq;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;

namespace Stock.Presentation.Kafka;

public static class AddKafkaClass
{
    public static void AddKafka(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<KafkaOptions>()
            .BindConfiguration(KafkaOptions.Name)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddOptions<DeadLetterOptions>()
            .BindConfiguration(DeadLetterOptions.Name)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton<IKafkaPublisher, KafkaPublisher>();
        builder.Services.AddSingleton<IKafkaConsumerFactory, KafkaConsumerFactory>();
        builder.Services.AddSingleton<IKafkaProducerFactory, KafkaProducerFactory>();

        builder.Services.AddHostedService<KafkaTopicsInitializer>();

        builder.Services.AddHostedService<StockCreatedConsumer>();
        builder.Services.AddHostedService<StockToggleStatusEventConsumer>();
        builder.Services.AddHostedService<PriceChangedConsumer>();
        builder.Services.AddHostedService<PriceChangeRequestedConsumer>();
        builder.Services.AddHostedService<NameChangedConsumer>();
        builder.Services.AddHostedService<TimeChangedConsumer>();

        builder.Services.AddSingleton<IDeadLetterPublisher, DeadLetterPublisher>();

        builder.Services.AddSingleton<IDlqEventToCommandMapper<StockCreatedEvent, CreateReadModelCommand>,
            StockCreatedDlqEventToCommandMapper>();
        builder.Services.AddSingleton<IDlqEventToCommandMapper<StockToggledStatusEvent, ToggleStatusReadModelCommand>,
            StockToggledStatusDlqEventToCommandMapper>();
        builder.Services.AddSingleton<IDlqEventToCommandMapper<PriceChangeRequestedEvent, ChangePriceCommand>,
            PriceChangeRequestedDlqEventToCommandMapper>();
        builder.Services.AddSingleton<IDlqEventToCommandMapper<PriceChangedEvent, ReplayReadModelCommand>,
            PriceChangedDlqEventToCommandMapper>();
        builder.Services.AddSingleton<IDlqEventToCommandMapper<NameChangedEvent, UpdateNameReadModelCommand>,
            NameChangedDlqEventToCommandMapper>();
        builder.Services.AddSingleton<IDlqEventToCommandMapper<TimeChangedEvent, UpdateTradingTimeCommand>,
            TimeChangedDlqEventToCommandMapper>();

        builder.Services.AddHostedService<StockCreatedDlqConsumer>();
        builder.Services.AddHostedService<StockToggledStatusDlqConsumer>();
        builder.Services.AddHostedService<PriceChangeRequestedDlqConsumer>();
        builder.Services.AddHostedService<PriceChangedDlqConsumer>();
        builder.Services.AddHostedService<NameChangedDlqConsumer>();
        builder.Services.AddHostedService<TimeChangedDlqConsumer>();
    }
}