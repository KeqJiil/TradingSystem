using Confluent.Kafka;
using Stock.Application.Events;

namespace Stock.Infrastructure.Messaging.Workers;

public class StockCreatedConsumer(IKafkaConsumerFactory consumerFactory) : BackgroundService
{
    private readonly IConsumer<string, StockCreatedEvent> _consumer =
        consumerFactory.Create<StockCreatedEvent>(groupId: "stock-created-events-group", clientId: "stock-created-events-consumer");
    
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