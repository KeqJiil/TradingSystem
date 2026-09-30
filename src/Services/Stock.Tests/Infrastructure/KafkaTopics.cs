using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Stock.Tests.Infrastructure;

public static class KafkaTopics
{
    public static async Task CreateWithMaxMessageBytesAsync(string bootstrapAddress, string topic, int maxMessageBytes)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapAddress }).Build();

        await admin.CreateTopicsAsync([
            new TopicSpecification
            {
                Name = topic,
                NumPartitions = 1,
                ReplicationFactor = 1,
                Configs = new Dictionary<string, string> { ["max.message.bytes"] = maxMessageBytes.ToString() }
            }
        ]);
    }

    public static async Task SetMaxMessageBytesAsync(string bootstrapAddress, string topic, int maxMessageBytes)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapAddress }).Build();

        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, List<ConfigEntry>>
        {
            [new ConfigResource { Type = ResourceType.Topic, Name = topic }] =
            [
                new ConfigEntry
                {
                    Name = "max.message.bytes",
                    Value = maxMessageBytes.ToString(),
                    IncrementalOperation = AlterConfigOpType.Set
                }
            ]
        });
    }
}
