using Confluent.Kafka;

namespace Stock.Infrastructure.Messaging.Workers;

public class StockEventConsumer(IKafkaConsumerFactory consumerFactory) : BackgroundService
{
    private readonly IConsumer<string, string> _consumer =
        consumerFactory.Create(groupId: "stock-events-group", clientId: "stock-events-consumer");

    
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