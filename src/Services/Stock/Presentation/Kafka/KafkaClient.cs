using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Messaging.Workers;
using Stock.Presentation.Options;

namespace Stock.Presentation.Kafka;

public static class AddKafkaClass
{
    public static void AddKafka(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<KafkaOptions>()
            .BindConfiguration(KafkaOptions.Name);
        
        builder.Services.AddSingleton<IProducer<string, string>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<KafkaOptions>>().Value;
            return new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = options.BootstrapServers,
                ClientId = options.ProducerClientId,
            }).Build();
        });
        
        builder.Services.AddSingleton<IKafkaConsumerFactory, KafkaConsumerFactory>();
        builder.Services.AddHostedService<StockEventConsumer>();
        builder.Services.AddHostedService<PriceChangeConsumer>();
    }
}