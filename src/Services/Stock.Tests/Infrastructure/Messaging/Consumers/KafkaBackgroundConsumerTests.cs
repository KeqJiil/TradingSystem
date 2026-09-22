using System.Collections.Concurrent;
using Confluent.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Messaging.Consumers;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;
using Stock.Infrastructure.Serialization;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Consumers;

public class KafkaBackgroundConsumerTests : IClassFixture<KafkaFixture>, IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(45);

    private readonly KafkaFixture _fixture;
    private readonly IOptions<KafkaOptions> _kafkaOptions;
    private readonly KafkaConsumerFactory _consumerFactory;
    private readonly KafkaProducerFactory _producerFactory;
    private readonly DeadLetterPublisher _dlq;

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
        _dlq = new DeadLetterPublisher(_producerFactory, Options.Create(new DeadLetterOptions()));
    }

    public void Dispose()
    {
        _dlq.Dispose();
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

        var fatal = ConsumeFirst<PriceChangedEvent>(FatalTopic(topic));
        Assert.Equal(poison, fatal.Message.Value.AggregateId);
        Assert.Equal(5, BitConverter.ToInt32(fatal.Message.Headers.GetLastBytes("attempt-count")));
    }

    [Fact]
    public async Task UndeserializableMessage_IsMovedToFatalAsRawBytes_AndConsumerKeepsGoing()
    {
        var topic = UniqueTopic();
        var group = UniqueGroup();
        var received = new ConcurrentQueue<Guid>();
        var good = Guid.NewGuid();
        var garbage = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x01 };

        await using var consumer = CreateConsumer(topic, group, message =>
        {
            received.Enqueue(message.AggregateId);
            return Task.FromResult(true);
        });

        ProduceRaw(topic, garbage);
        Produce(topic, good);

        await consumer.StartAsync(CancellationToken.None);
        await Polling.WaitUntilAsync(() => Task.FromResult(received.Contains(good)), Timeout);
        await consumer.StopAsync(CancellationToken.None);

        Assert.Equal([good], received);
        Assert.Equal(2L, CommittedOffset(topic, group));

        var fatal = ConsumeFirst<byte[]>(FatalTopic(topic));
        Assert.Equal(garbage, fatal.Message.Value);
    }

    [Fact]
    public async Task Tombstone_IsCommittedWithoutCallingHandler()
    {
        var topic = UniqueTopic();
        var group = UniqueGroup();
        var received = new ConcurrentQueue<Guid>();
        var good = Guid.NewGuid();

        await using var consumer = CreateConsumer(topic, group, message =>
        {
            received.Enqueue(message.AggregateId);
            return Task.FromResult(true);
        });

        ProduceRaw(topic, null);
        Produce(topic, good);

        await consumer.StartAsync(CancellationToken.None);
        await Polling.WaitUntilAsync(() => Task.FromResult(received.Contains(good)), Timeout);
        await consumer.StopAsync(CancellationToken.None);

        Assert.Equal([good], received);
        Assert.Equal(2L, CommittedOffset(topic, group));
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

    [Fact]
    public async Task HoldCommitWhenDeferred_KeepsReading_ButCommitsOnlyWhenHandlerAllows()
    {
        var topic = UniqueTopic();
        var group = UniqueGroup();
        var received = new ConcurrentQueue<Guid>();
        var deferred = Guid.NewGuid();
        var alsoDeferred = Guid.NewGuid();
        var releasing = Guid.NewGuid();

        await using (var consumer = CreateConsumer(topic, group, message =>
                     {
                         received.Enqueue(message.AggregateId);
                         return Task.FromResult(message.AggregateId == releasing);
                     }, holdCommitWhenDeferred: true))
        {
            Produce(topic, deferred);
            Produce(topic, alsoDeferred);

            await consumer.StartAsync(CancellationToken.None);
            await Polling.WaitUntilAsync(() => Task.FromResult(received.Contains(alsoDeferred)), Timeout);
            await consumer.StopAsync(CancellationToken.None);

            Assert.Equal([deferred, alsoDeferred], received);
            Assert.Equal(Offset.Unset.Value, CommittedOffset(topic, group));
        }

        received.Clear();
        await using (var consumer = CreateConsumer(topic, group, message =>
                     {
                         received.Enqueue(message.AggregateId);
                         return Task.FromResult(message.AggregateId == releasing);
                     }, holdCommitWhenDeferred: true))
        {
            Produce(topic, releasing);

            await consumer.StartAsync(CancellationToken.None);
            await Polling.WaitUntilAsync(() => Task.FromResult(received.Contains(releasing)), Timeout);
            await consumer.StopAsync(CancellationToken.None);

            Assert.Equal([deferred, alsoDeferred, releasing], received);
            Assert.Equal(3L, CommittedOffset(topic, group));
        }
    }

    private TestConsumer CreateConsumer(string topic, string group, Func<PriceChangedEvent, Task<bool>> handle,
        bool holdCommitWhenDeferred = false)
    {
        return new TestConsumer(_consumerFactory, _dlq, topic, group, handle, holdCommitWhenDeferred);
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

    private void ProduceRaw(string topic, byte[]? value)
    {
        using var producer = _producerFactory.CreateRaw($"seed-{Guid.NewGuid()}");
        producer.Produce(topic, new Message<string, byte[]> { Key = Guid.NewGuid().ToString(), Value = value! });
        producer.Flush(TimeSpan.FromSeconds(10));
    }

    private ConsumeResult<string, TValue> ConsumeFirst<TValue>(string topic)
    {
        var builder = new ConsumerBuilder<string, TValue>(new ConsumerConfig
        {
            BootstrapServers = _fixture.BootstrapAddress,
            GroupId = $"monitor-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Earliest
        });
        if (typeof(TValue) != typeof(byte[]))
            builder.SetValueDeserializer(new ProtobufNetDeserializer<TValue>());

        using var consumer = builder.Build();
        consumer.Subscribe(topic);
        var result = consumer.Consume(TimeSpan.FromSeconds(30));
        Assert.NotNull(result);
        consumer.Close();
        return result;
    }

    private static string FatalTopic(string topic)
    {
        return topic + new DeadLetterOptions().TopicSuffix + ".fatal";
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
        IDeadLetterPublisher dlq,
        string topic,
        string group,
        Func<PriceChangedEvent, Task<bool>> handle,
        bool holdCommitWhenDeferred)
        : KafkaBackgroundConsumer<PriceChangedEvent>(consumerFactory, NullLogger<TestConsumer>.Instance, dlq),
            IAsyncDisposable
    {
        protected override string GroupId => group;

        protected override string ClientId => $"{group}-client";

        protected override string Topic => topic;

        protected override bool HoldCommitWhenDeferred => holdCommitWhenDeferred;

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