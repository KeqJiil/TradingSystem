using Confluent.Kafka;
using Stock.Application.Events;

namespace Stock.Infrastructure.Messaging.Workers;

public class StockEventConsumer(IKafkaConsumerFactory consumerFactory) : BackgroundService
{
    private readonly IConsumer<string, StockCreatedEvent> _consumer =
        consumerFactory.Create<StockCreatedEvent>(groupId: "stock-events-group", clientId: "stock-events-consumer");

    
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