using Confluent.Kafka;

namespace Stock.Infrastructure.Messaging.Workers;

public class StockEventProducer(IProducer<string, string> producer) : BackgroundService
{
    
}