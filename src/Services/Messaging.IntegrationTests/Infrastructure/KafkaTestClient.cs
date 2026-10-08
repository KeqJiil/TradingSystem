using System.Text;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Messaging.IntegrationTests.Infrastructure;

public sealed record ReadMessage(string? Key, byte[] Value, IReadOnlyDictionary<string, string> Headers)
{
    public string Text => Encoding.UTF8.GetString(Value);
}

public sealed class KafkaTestClient(string bootstrapServers)
{
    public async Task ProduceAsync(string topic, string? key, byte[] value,
        IDictionary<string, string>? headers = null)
    {
        using var producer = new ProducerBuilder<string, byte[]>(
            new ProducerConfig { BootstrapServers = bootstrapServers }).Build();

        var kafkaHeaders = new Headers();
        foreach (var (name, headerValue) in headers ?? new Dictionary<string, string>())
            kafkaHeaders.Add(name, Encoding.UTF8.GetBytes(headerValue));

        await producer.ProduceAsync(topic, new Message<string, byte[]>
        {
            Key = key!,
            Value = value,
            Headers = kafkaHeaders
        });
    }

    public Task<List<ReadMessage>> ReadAsync(string topic, int count, TimeSpan? timeout = null)
    {
        return Task.Run(() => Read(topic, count, timeout ?? Wait.Default));
    }

    public List<string> ListTopics()
    {
        using var admin = CreateAdmin();

        return admin.GetMetadata(TimeSpan.FromSeconds(10)).Topics.Select(t => t.Topic).ToList();
    }

    public async Task<string?> GetTopicConfigAsync(string topic, string name)
    {
        using var admin = CreateAdmin();

        var results = await admin.DescribeConfigsAsync(
            [new ConfigResource { Type = ResourceType.Topic, Name = topic }]);

        return results.Single().Entries.TryGetValue(name, out var entry) ? entry.Value : null;
    }

    private List<ReadMessage> Read(string topic, int count, TimeSpan timeout)
    {
        using var consumer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = TestNames.Unique("reader"),
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

            var headers = result.Message.Headers.ToDictionary(
                h => h.Key, h => Encoding.UTF8.GetString(h.GetValueBytes()));
            messages.Add(new ReadMessage(result.Message.Key, result.Message.Value, headers));
        }

        return messages;
    }

    private IAdminClient CreateAdmin()
    {
        return new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();
    }
}
