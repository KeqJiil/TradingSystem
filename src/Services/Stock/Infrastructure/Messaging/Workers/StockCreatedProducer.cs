using Confluent.Kafka;

namespace Stock.Infrastructure.Messaging.Workers;

public class StockCreatedProducer(IProducer<string, string> producer) : BackgroundService
{
    
}