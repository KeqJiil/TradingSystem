using Confluent.Kafka;

namespace Stock.Infrastructure.Messaging.Kafka;

public class KafkaClient(IConfiguration config)
{
    private readonly ProducerConfig _producerConfig = new() 
    {
        BootstrapServers = "kafka:9092",
        ClientId = config["ProducerClientId"],
        // Compression in future
    };
    
    private readonly ConsumerConfig _consumerConfig = new()
    {
        BootstrapServers = "kafka:9092",
        GroupId = config["ConsumerGroupId"],
        ClientId = config["ConsumerClientId"],
        EnableAutoCommit = false,
        EnableAutoOffsetStore = false,
        GroupProtocol = GroupProtocol.Consumer,
        
    };
}