using System.Text;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Messaging.Testing;

public sealed record ReadMessage(string? Key, byte[] Value, IReadOnlyDictionary<string, string> Headers);

public sealed class KafkaReader(string bootstrapServers)
{
    public async Task<string> CreateTopicAsync()
    {
        var name = $"outbox-{Guid.NewGuid():N}";
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();
        await admin.CreateTopicsAsync([new TopicSpecification { Name = name, NumPartitions = 1, ReplicationFactor = 1 }]);
        return name;
    }

    public Task<List<ReadMessage>> ReadAsync(string topic, int count, TimeSpan? timeout = null)
    {
        return Task.Run(() => Read(topic, count, timeout ?? Wait.Default));
    }

    private List<ReadMessage> Read(string topic, int count, TimeSpan timeout)
    {
        using var consumer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = $"reader-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        }).Build();
        consumer.Subscribe(topic);

        var messages = new List<ReadMessage>();
        var limit = DateTime.UtcNow + timeout;

        while (messages.Count < count && DateTime.UtcNow < limit)
        {
            var result = consumer.Consume(TimeSpan.FromMilliseconds(200));
            if (result is null) continue;

            var headers = new Dictionary<string, string>();
            foreach (var header in result.Message.Headers)
                headers[header.Key] = Encoding.UTF8.GetString(header.GetValueBytes());

            messages.Add(new ReadMessage(result.Message.Key, result.Message.Value, headers));
        }

        consumer.Close();
        return messages;
    }
}
