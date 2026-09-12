using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;
using Stock.Infrastructure.Serialization;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Publishers;

public class DeadLetterPublisherTests : IClassFixture<KafkaFixture>, IAsyncLifetime
{
    private readonly string _sourceTopic = $"price-topic-{Guid.NewGuid()}";

    private readonly KafkaFixture _fixture;
    private readonly DeadLetterOptions _options = new();
    private DeadLetterPublisher _sut = null!;

    public DeadLetterPublisherTests(KafkaFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        var kafkaOptions = Options.Create(new KafkaOptions
        {
            BootstrapServers = _fixture.BootstrapAddress,
            ProducerClientId = "dlq-publisher-tests"
        });

        _sut = new DeadLetterPublisher(new KafkaProducerFactory(kafkaOptions), Options.Create(_options));
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PublishAsync_WithRetryableException_SendsToRetryTopic()
    {
        var value = new PriceChangedEvent(Guid.NewGuid(), 5m, 1, DateTimeOffset.UtcNow);
        var expectedTopic = _sourceTopic + _options.TopicSuffix + ".retry";

        await _sut.PublishAsync(_sourceTopic, value, new TimeoutException("broker slow"), 2, CancellationToken.None);

        var result = Consume<PriceChangedEvent>(expectedTopic);

        Assert.Equal(value.AggregateId.ToString(), result.Message.Key);
        Assert.Equal(_sourceTopic, GetHeaderString(result.Message.Headers, "original-topic"));
        Assert.Equal(2, BitConverter.ToInt32(result.Message.Headers.GetLastBytes("attempt-count")));
        Assert.Contains("broker slow", GetHeaderString(result.Message.Headers, "exception-message"));
    }

    [Fact]
    public async Task PublishAsync_WithNonRetryableException_SendsToFatalTopic()
    {
        var value = new PriceChangedEvent(Guid.NewGuid(), 5m, 1, DateTimeOffset.UtcNow);
        var expectedTopic = _sourceTopic + _options.TopicSuffix + ".fatal";

        await _sut.PublishAsync(_sourceTopic, value, new InvalidOperationException("bad payload"), 1,
            CancellationToken.None);

        var result = Consume<PriceChangedEvent>(expectedTopic);

        Assert.Equal(value.AggregateId.ToString(), result.Message.Key);
        Assert.Equal(_sourceTopic, GetHeaderString(result.Message.Headers, "original-topic"));
    }

    [Fact]
    public async Task PublishAsync_WithExplicitRetryableFlag_SendsToTopicByFlag_NotByExceptionType()
    {
        var value = new PriceChangedEvent(Guid.NewGuid(), 5m, 1, DateTimeOffset.UtcNow);
        var expectedTopic = _sourceTopic + _options.TopicSuffix + ".retry";

        await _sut.PublishAsync(_sourceTopic, value, true, 3, CancellationToken.None);

        var result = Consume<PriceChangedEvent>(expectedTopic);

        Assert.Equal(3, BitConverter.ToInt32(result.Message.Headers.GetLastBytes("attempt-count")));
    }

    [Fact]
    public async Task PublishUnknownAsync_SendsToUnknownTopic()
    {
        var value = new PriceChangedEvent(Guid.NewGuid(), 5m, 1, DateTimeOffset.UtcNow);

        await _sut.PublishUnknownAsync(value, CancellationToken.None);

        var result = Consume<PriceChangedEvent>(_options.UnknownTopic);

        Assert.Equal(value, result.Message.Value);
    }

    private ConsumeResult<string, TValue> Consume<TValue>(string topic)
    {
        using var consumer = new ConsumerBuilder<string, TValue>(new ConsumerConfig
            {
                BootstrapServers = _fixture.BootstrapAddress,
                GroupId = Guid.NewGuid().ToString(),
                AutoOffsetReset = AutoOffsetReset.Earliest
            })
            .SetValueDeserializer(new ProtobufNetDeserializer<TValue>())
            .Build();

        consumer.Subscribe(topic);
        var result = consumer.Consume(TimeSpan.FromSeconds(30));
        Assert.NotNull(result);
        consumer.Close();
        return result!;
    }

    private static string GetHeaderString(Headers headers, string key)
    {
        return Encoding.UTF8.GetString(headers.GetLastBytes(key));
    }
}