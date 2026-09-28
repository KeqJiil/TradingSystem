using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Dapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Handlers;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Messaging.Workers;
using Stock.Infrastructure.Options;
using Stock.Infrastructure.Persistence;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Infrastructure.Serialization;
using Xunit;
using AppEvents = Stock.Application.Events;

namespace Stock.Tests.Infrastructure.Messaging.Workers;

public class OutboxDispatcherServiceTests : IClassFixture<KafkaFixture>, IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly KafkaFixture _kafkaFixture;
    private readonly MssqlFixture _mssqlFixture;
    private readonly DeadLetterOptions _dlqOptions = new();

    private TestDbContext _dbContext = null!;
    private TestDbContext _serviceDbContext = null!;
    private ServiceProvider _provider = null!;
    private OutboxDispatcherService _service = null!;
    private KafkaPublisher _publisher = null!;

    public OutboxDispatcherServiceTests(KafkaFixture kafkaFixture, MssqlFixture mssqlFixture)
    {
        _kafkaFixture = kafkaFixture;
        _mssqlFixture = mssqlFixture;
    }

    public async Task InitializeAsync()
    {
        _dbContext = new TestDbContext(_mssqlFixture.ConnectionString);
        await _dbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(_dbContext);
        _serviceDbContext = new TestDbContext(_mssqlFixture.ConnectionString);

        var kafkaOptions = Options.Create(new KafkaOptions
        {
            BootstrapServers = _kafkaFixture.BootstrapAddress,
            ProducerClientId = "outbox-dispatcher-tests"
        });
        _publisher = new KafkaPublisher(
            new KafkaProducerFactory(kafkaOptions, NullLogger<KafkaProducerFactory>.Instance), kafkaOptions,
            NullLogger<KafkaPublisher>.Instance);

        var services = new ServiceCollection();
        services.AddSingleton<IOutboxReader>(new OutboxReader(_serviceDbContext));
        services.AddSingleton<IOutboxMarker>(new OutboxWriter(_serviceDbContext));
        services.AddSingleton(new StockEventKafkaHandler(_publisher));
        services.AddSingleton<INotificationHandler<AppEvents.StockCreatedEvent>>(sp =>
            sp.GetRequiredService<StockEventKafkaHandler>());
        services.AddSingleton<INotificationHandler<AppEvents.StockToggledStatusEvent>>(sp =>
            sp.GetRequiredService<StockEventKafkaHandler>());
        services.AddSingleton<INotificationHandler<AppEvents.PriceChangedEvent>>(sp =>
            sp.GetRequiredService<StockEventKafkaHandler>());
        services.AddLogging();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(IMediator).Assembly));

        _provider = services.BuildServiceProvider();

        var dlq = new DeadLetterPublisher(_publisher, Options.Create(_dlqOptions),
            NullLogger<DeadLetterPublisher>.Instance);

        _service = new OutboxDispatcherService(
            NullLogger<OutboxDispatcherService>.Instance,
            _provider.GetRequiredService<IServiceScopeFactory>(),
            dlq);
    }

    public async Task DisposeAsync()
    {
        await _service.StopAsync(CancellationToken.None);
        _service.Dispose();
        _publisher.Dispose();
        await _provider.DisposeAsync();
        await _serviceDbContext.DisposeAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task ExecuteAsync_PendingRecord_PublishesToKafkaAndMarksCompleted()
    {
        var aggregateId = Guid.NewGuid();
        var id = await InsertOutboxRowAsync(
            EventTypeNames.StockToggledStatus,
            JsonSerializer.Serialize(new AppEvents.StockToggledStatusEvent(aggregateId, DateTimeOffset.UtcNow, false, 1)));

        await EnsureTopicsExistAsync(TopicNames.StockStatusToggled);
        await _service.StartAsync(CancellationToken.None);

        var result = Consume<StockToggledStatusEvent>(TopicNames.StockStatusToggled,
            m => m.AggregateId == aggregateId);
        Assert.Equal(aggregateId, result.Message.Value.AggregateId);

        var status = await WaitForStatusAsync(id, "COMPLETED");
        Assert.Equal("COMPLETED", status);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownEventType_SendsToDlqUnknownTopicAndMarksCompleted()
    {
        var id = await InsertOutboxRowAsync("SomeUnknownEvent", "{}");

        await EnsureTopicsExistAsync(_dlqOptions.UnknownTopic);
        await _service.StartAsync(CancellationToken.None);

        var result = ConsumeJson<OutboxData>(_dlqOptions.UnknownTopic, m => m.Id == id);
        Assert.Equal(id, result.Message.Value.Id);

        var status = await WaitForStatusAsync(id, "COMPLETED");
        Assert.Equal("COMPLETED", status);
    }

    [Fact]
    public async Task ExecuteAsync_MalformedPayloadForKnownEventType_SendsToUnknownDlqAndMarksCompleted()
    {
        var id = await InsertOutboxRowAsync(EventTypeNames.StockToggledStatus, "not-valid-json");

        await _service.StartAsync(CancellationToken.None);

        var result = ConsumeJson<OutboxData>(_dlqOptions.UnknownTopic, m => m.Id == id);
        Assert.Equal(id, result.Message.Value.Id);

        var status = await WaitForStatusAsync(id, "COMPLETED");
        Assert.Equal("COMPLETED", status);
    }

    [Fact]
    public async Task ExecuteAsync_RetryAttemptsExhausted_SendsToOwnFatalDlqAndMarksCompleted()
    {
        var aggregateId = Guid.NewGuid();
        var fatalTopic = TopicNames.StockStatusToggled + _dlqOptions.TopicSuffix + ".fatal";
        var id = await InsertOutboxRowAsync(
            EventTypeNames.StockToggledStatus,
            JsonSerializer.Serialize(new AppEvents.StockToggledStatusEvent(aggregateId, DateTimeOffset.UtcNow, false, 1)),
            attempts: 3);

        await EnsureTopicsExistAsync(fatalTopic);
        await _service.StartAsync(CancellationToken.None);

        var result = Consume<StockToggledStatusEvent>(fatalTopic, m => m.AggregateId == aggregateId);
        Assert.Equal(4, BitConverter.ToInt32(result.Message.Headers.GetLastBytes("attempt-count")));

        var status = await WaitForStatusAsync(id, "COMPLETED");
        Assert.Equal("COMPLETED", status);
    }

    [Fact]
    public async Task ExecuteAsync_RetryAttemptsExhausted_AndOwnDlqRejects_SendsToUnknownDlqAndMarksCompleted()
    {
        var fatalTopic = TopicNames.StockCreated + _dlqOptions.TopicSuffix + ".fatal";
        await KafkaTopics.CreateWithMaxMessageBytesAsync(_kafkaFixture.BootstrapAddress, fatalTopic, 100);
        await EnsureTopicsExistAsync(_dlqOptions.UnknownTopic);
        var id = await InsertOutboxRowAsync(
            EventTypeNames.StockCreated,
            JsonSerializer.Serialize(new AppEvents.StockCreatedEvent(Guid.NewGuid(), "AAPL", true, "USD",
                new TimeOnly(9, 30), new TimeOnly(16, 0))),
            attempts: 3);

        await _service.StartAsync(CancellationToken.None);

        var result = ConsumeJson<OutboxData>(_dlqOptions.UnknownTopic, m => m.Id == id);
        Assert.Equal(EventTypeNames.StockCreated, result.Message.Value.EventType);

        var status = await WaitForStatusAsync(id, "COMPLETED");
        Assert.Equal("COMPLETED", status);
    }

    [Fact]
    public async Task ExecuteAsync_PublishFails_LeavesRecordNotCompleted_WithAttemptCounted()
    {
        var oversizedName = new string('A', 2_000_000);
        var id = await InsertOutboxRowAsync(
            EventTypeNames.StockCreated,
            JsonSerializer.Serialize(new AppEvents.StockCreatedEvent(Guid.NewGuid(), oversizedName, true, "USD",
                new TimeOnly(9, 30), new TimeOnly(16, 0))));

        await EnsureTopicsExistAsync(TopicNames.StockCreated);
        await _service.StartAsync(CancellationToken.None);

        await Polling.WaitUntilAsync(async () => (await ReadAttemptsAsync(id)) == 1, TimeSpan.FromSeconds(10));
        await Polling.StaysTrueAsync(async () => await ReadStatusAsync(id) == "PROCESSING", TimeSpan.FromSeconds(3));
        Assert.Equal(1, await ReadAttemptsAsync(id));
    }

    [Fact]
    public async Task ExecuteAsync_ReaderFails_KeepsDispatchingOnNextCycle()
    {
        await TestDatabase.WithTableOfflineAsync(_dbContext, "outbox", async () =>
        {
            await _service.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(3));
        });

        var aggregateId = Guid.NewGuid();
        var id = await InsertOutboxRowAsync(
            EventTypeNames.StockToggledStatus,
            JsonSerializer.Serialize(new AppEvents.StockToggledStatusEvent(aggregateId, DateTimeOffset.UtcNow, true, 1)));
        await EnsureTopicsExistAsync(TopicNames.StockStatusToggled);

        var status = await WaitForStatusAsync(id, "COMPLETED");
        Assert.Equal("COMPLETED", status);
    }

    [Fact]
    public async Task ExecuteAsync_NoPendingRecords_DoesNotThrow()
    {
        await _service.StartAsync(CancellationToken.None);

        await Task.Delay(TimeSpan.FromSeconds(3));

        await _service.StopAsync(CancellationToken.None);
    }

    private async Task EnsureTopicsExistAsync(params string[] topics)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig
            {
                BootstrapServers = _kafkaFixture.BootstrapAddress
            })
            .Build();

        try
        {
            await admin.CreateTopicsAsync(topics.Select(t => new TopicSpecification
            {
                Name = t,
                NumPartitions = 1,
                ReplicationFactor = 1
            }));
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
        }
    }

    private async Task<Guid> InsertOutboxRowAsync(string eventType, string payload, int attempts = 0)
    {
        var id = Guid.NewGuid();

        await _dbContext.Connection.ExecuteAsync("""
                                                 INSERT INTO outbox (id, aggregate_id, payload, event_type, status, attempts)
                                                 VALUES (@Id, @AggregateId, @Payload, @EventType, 'PENDING', @Attempts)
                                                 """, new
        {
            Id = id,
            AggregateId = Guid.NewGuid(),
            Payload = payload,
            EventType = eventType,
            Attempts = attempts
        });

        return id;
    }

    private Task<string> ReadStatusAsync(Guid id)
    {
        return _dbContext.Connection.QuerySingleAsync<string>("SELECT status FROM outbox WHERE id = @Id", new { Id = id });
    }

    private Task<int> ReadAttemptsAsync(Guid id)
    {
        return _dbContext.Connection.QuerySingleAsync<int>("SELECT attempts FROM outbox WHERE id = @Id", new { Id = id });
    }

    private async Task<string> WaitForStatusAsync(Guid id, string expectedStatus)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        string status;
        do
        {
            status = await _dbContext.Connection.QuerySingleAsync<string>(
                "SELECT status FROM outbox WHERE id = @Id", new { Id = id });

            if (status == expectedStatus)
                return status;

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        } while (DateTime.UtcNow < deadline);

        return status;
    }

    private ConsumeResult<string, TValue> Consume<TValue>(string topic, Func<TValue, bool> predicate)
    {
        return ConsumeMatching(topic, new ProtobufNetDeserializer<TValue>(), predicate);
    }

    private ConsumeResult<string, TValue> ConsumeJson<TValue>(string topic, Func<TValue, bool> predicate)
    {
        return ConsumeMatching(topic, new KafkaJsonDeserializer<TValue>(), predicate);
    }

    private ConsumeResult<string, TValue> ConsumeMatching<TValue>(string topic, IDeserializer<TValue> deserializer,
        Func<TValue, bool> predicate)
    {
        using var consumer = new ConsumerBuilder<string, TValue>(new ConsumerConfig
            {
                BootstrapServers = _kafkaFixture.BootstrapAddress,
                GroupId = Guid.NewGuid().ToString(),
                AutoOffsetReset = AutoOffsetReset.Earliest
            })
            .SetValueDeserializer(deserializer)
            .Build();

        consumer.Subscribe(topic);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var result = consumer.Consume(TimeSpan.FromSeconds(1));
            if (result is not null && predicate(result.Message.Value))
            {
                consumer.Close();
                return result;
            }
        }

        consumer.Close();
        throw new TimeoutException($"No matching message observed on topic '{topic}' within 30s.");
    }
}