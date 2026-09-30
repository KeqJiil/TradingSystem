using Confluent.Kafka;

namespace Stock.Infrastructure.Messaging.Publishers;

public interface IKafkaPublisher
{
    Task PublishAsync(string topic, Message<string, byte[]> message, CancellationToken ct);
}