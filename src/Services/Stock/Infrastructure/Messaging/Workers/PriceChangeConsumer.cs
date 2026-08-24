using Confluent.Kafka;
using Stock.Application.Events;

namespace Stock.Infrastructure.Messaging.Workers;

public class PriceChangeConsumer(IKafkaConsumerFactory consumerFactory) : BackgroundService
{
    private readonly IConsumer<string, PriceChangedEvent> _consumer =
        consumerFactory.Create<PriceChangedEvent>(groupId: "price-change-events-group", clientId: "price-change-events-consumer");
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Factory.StartNew(
            () => Consume(stoppingToken),
            stoppingToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private async Task Consume(CancellationToken ct)
    {
        
    }
}