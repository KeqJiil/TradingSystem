using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Stock.Application.Abstractions;
using Stock.Application.Events;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Messaging.Consumers;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Presentation.Options;

namespace Stock.Presentation.Kafka;

public static class AddKafkaClass
{
    public static void AddKafka(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<KafkaOptions>()
            .BindConfiguration(KafkaOptions.Name);
        builder.Services.AddOptions<DeadLetterOptions>().BindConfiguration(DeadLetterOptions.Name).ValidateOnStart();

        builder.Services.AddSingleton<IProducer<string, string>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<KafkaOptions>>().Value;
            return new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = options.BootstrapServers,
                ClientId = options.ProducerClientId
            }).Build();
        });

        builder.Services.AddSingleton<IKafkaConsumerFactory, KafkaConsumerFactory>();
        builder.Services.AddSingleton<IKafkaProducerFactory, KafkaProducerFactory>();

        builder.Services.AddHostedService<StockCreatedConsumer>();
        builder.Services.AddHostedService<StockToggleStatusEventConsumer>();
        builder.Services.AddHostedService<PriceChangedConsumer>();
        builder.Services.AddHostedService<PriceChangeRequestedConsumer>();

        builder.Services.AddSingleton<IProducer<string, PriceChangedEvent>>(sp =>
            sp.GetRequiredService<IKafkaProducerFactory>().Create<PriceChangedEvent>("stock-price-producer"));

        builder.Services.AddSingleton<IProducer<string, StockToggledStatusEvent>>(sp =>
            sp.GetRequiredService<IKafkaProducerFactory>().Create<StockToggledStatusEvent>("stock-status-producer"));

        builder.Services.AddSingleton<IProducer<string, StockCreatedEvent>>(sp =>
            sp.GetRequiredService<IKafkaProducerFactory>().Create<StockCreatedEvent>("stock-created-producer"));

        builder.Services.AddSingleton<IDeadLetterPublisher, DeadLetterPublisher>();
    }
}