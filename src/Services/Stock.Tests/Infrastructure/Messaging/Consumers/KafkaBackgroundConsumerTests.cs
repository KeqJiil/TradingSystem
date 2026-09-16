using System.Collections.Concurrent;
using Confluent.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Messaging.Consumers;
using Stock.Infrastructure.Options;
using Stock.Infrastructure.Serialization;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Consumers;

public class KafkaBackgroundConsumerTests : IClassFixture<KafkaFixture>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(45);

    private readonly KafkaFixture _fixture;
    private readonly IOptions<KafkaOptions> _kafkaOptions;
    private readonly KafkaConsumerFactory _consumerFactory;
    private readonly KafkaProducerFactory _producerFactory;

    public KafkaBackgroundConsumerTests(KafkaFixture fixture)
    {
        _fixture = fixture;
        _kafkaOptions = Options.Create(new KafkaOptions
        {
            BootstrapServers = fixture.BootstrapAddress,
            ProducerClientId = "consumer-base-tests"
        });
        _consumerFactory = new KafkaConsumerFactory(_kafkaOptions);
        _producerFactory = new KafkaProducerFactory(_kafkaOptions);
    }

    [Fact]
    public async Task ConsumeLoop_ShouldKeepRunning_AfterHandlerThrows()
    {
        var topic = UniqueTopic();
        var received = new ConcurrentQueue<Guid>();
        var poison = Guid.NewGuid();
        var good = Guid.NewGuid();

        await using var consumer = CreateConsumer(topic, UniqueGroup(), message =>
        {
            if (message.AggregateId == poison)
                throw new InvalidOperationException("simulated handler failure");

            received.Enqueue(message.AggregateId);
            return Task.FromResult(true);
        });

        Produce(topic, poison);
        Produce(topic, good);

        await consumer.StartAsync(CancellationToken.None);
        await Polling.WaitUntilAsync(() => Task.FromResult(received.Contains(good)), Timeout);

        Assert.Contains(good, received);
        Assert.DoesNotContain(poison, received);
    }

    [Fact]
    public async Task GracefulStop_ShouldCommitOffsetsOfHandledMessages()
    {
        var topic = UniqueTopic();
        var group = UniqueGroup();
        var received = new ConcurrentQueue<Guid>();
        var aggregateId = Guid.NewGuid();

        await using var consumer = CreateConsumer(topic, group, message =>
        {
            received.Enqueue(message.AggregateId);
            return Task.FromResult(true);
        });

        Produce(topic, aggregateId);

        await consumer.StartAsync(CancellationToken.None);
        await Polling.WaitUntilAsync(() => Task.FromResult(received.Contains(aggregateId)), Timeout);
        await consumer.StopAsync(CancellationToken.None);

        Assert.Equal(1L, CommittedOffset(topic, group));
    }

    [Fact]
    public async Task GracefulStop_ShouldNotCommitOffset_WhenHandlerDefersTheMessage()
    {
        var topic = UniqueTopic();
        var group = UniqueGroup();
        var received = new ConcurrentQueue<Guid>();
        var aggregateId = Guid.NewGuid();

        await using var consumer = CreateConsumer(topic, group, message =>
        {
            received.Enqueue(message.AggregateId);
            return Task.FromResult(false);
        });

        Produce(topic, aggregateId);

        await consumer.StartAsync(CancellationToken.None);
        await Polling.WaitUntilAsync(() => Task.FromResult(received.Contains(aggregateId)), Timeout);
        await consumer.StopAsync(CancellationToken.None);

        Assert.Equal(Offset.Unset.Value, CommittedOffset(topic, group));
    }

    private TestConsumer CreateConsumer(string topic, string group, Func<PriceChangedEvent, Task<bool>> handle)
    {
        return new TestConsumer(_consumerFactory, topic, group, handle);
    }

    private void Produce(string topic, Guid aggregateId)
    {
        using var producer = _producerFactory.Create<PriceChangedEvent>($"seed-{Guid.NewGuid()}");
        producer.Produce(topic, new Message<string, PriceChangedEvent>
        {
            Key = aggregateId.ToString(),
            Value = new PriceChangedEvent(aggregateId, 1m, 1, DateTimeOffset.UtcNow)
        });
        producer.Flush(TimeSpan.FromSeconds(10));
    }

    private long CommittedOffset(string topic, string group)
    {
        using var consumer = new ConsumerBuilder<string, PriceChangedEvent>(new ConsumerConfig
            {
                BootstrapServers = _fixture.BootstrapAddress,
                GroupId = group,
                EnableAutoCommit = false
            })
            .SetValueDeserializer(new ProtobufNetDeserializer<PriceChangedEvent>())
            .Build();

        var committed = consumer.Committed([new TopicPartition(topic, new Partition(0))], TimeSpan.FromSeconds(15));
        return committed.Single().Offset.Value;
    }

    private static string UniqueTopic()
    {
        return $"consumer-base-topic-{Guid.NewGuid()}";
    }

    private static string UniqueGroup()
    {
        return $"consumer-base-group-{Guid.NewGuid()}";
    }

    private sealed class TestConsumer(
        IKafkaConsumerFactory consumerFactory,
        string topic,
        string group,
        Func<PriceChangedEvent, Task<bool>> handle)
        : KafkaBackgroundConsumer<PriceChangedEvent>(consumerFactory, NullLogger<TestConsumer>.Instance),
            IAsyncDisposable
    {
        protected override string GroupId => group;

        protected override string ClientId => $"{group}-client";

        protected override string Topic => topic;

        protected override Task<bool> HandleAsync(PriceChangedEvent message, Headers headers, CancellationToken ct)
        {
            return handle(message);
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync(CancellationToken.None);
            Dispose();
        }
    }
}