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
    private ServiceProvider _provider = null!;
    private OutboxDispatcherService _service = null!;
    private readonly List<IDisposable> _producers = [];

    public OutboxDispatcherServiceTests(KafkaFixture kafkaFixture, MssqlFixture mssqlFixture)
    {
        _kafkaFixture = kafkaFixture;
        _mssqlFixture = mssqlFixture;
    }

    public async Task InitializeAsync()
    {
        _dbContext = new TestDbContext(_mssqlFixture.ConnectionString);
        await _dbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await OutboxTestSchema.EnsureCreatedAsync(_dbContext);
        await _dbContext.Connection.ExecuteAsync("DELETE FROM outbox");

        var kafkaProducerFactory = new KafkaProducerFactory(Options.Create(new KafkaOptions
        {
            BootstrapServers = _kafkaFixture.BootstrapAddress,
            ProducerClientId = "outbox-dispatcher-tests"
        }));

        var stockCreatedProducer = kafkaProducerFactory.Create<StockCreatedEvent>("stock-created-outbox-tests");
        var stockToggledStatusProducer =
            kafkaProducerFactory.Create<StockToggledStatusEvent>("stock-toggled-outbox-tests");
        var priceChangedProducer = kafkaProducerFactory.Create<PriceChangedEvent>("price-changed-outbox-tests");
        _producers.AddRange([stockCreatedProducer, stockToggledStatusProducer, priceChangedProducer]);

        var services = new ServiceCollection();
        services.AddSingleton<IOutboxReader>(new OutboxReader(_dbContext));
        services.AddSingleton<IOutboxMarker>(new OutboxWriter(_dbContext));
        services.AddSingleton(new StockEventKafkaHandler(stockCreatedProducer, stockToggledStatusProducer,
            priceChangedProducer));
        services.AddSingleton<INotificationHandler<AppEvents.StockCreatedEvent>>(sp =>
            sp.GetRequiredService<StockEventKafkaHandler>());
        services.AddSingleton<INotificationHandler<AppEvents.StockToggledStatusEvent>>(sp =>
            sp.GetRequiredService<StockEventKafkaHandler>());
        services.AddSingleton<INotificationHandler<AppEvents.PriceChangedEvent>>(sp =>
            sp.GetRequiredService<StockEventKafkaHandler>());
        services.AddLogging();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(IMediator).Assembly));

        _provider = services.BuildServiceProvider();

        var dlq = new DeadLetterPublisher(kafkaProducerFactory, Options.Create(_dlqOptions));

        _service = new OutboxDispatcherService(
            NullLogger<OutboxDispatcherService>.Instance,
            _provider.GetRequiredService<IServiceScopeFactory>(),
            dlq);
    }

    public async Task DisposeAsync()
    {
        await _service.StopAsync(CancellationToken.None);
        _service.Dispose();
        foreach (var producer in _producers)
            producer.Dispose();
        await _provider.DisposeAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task ExecuteAsync_PendingRecord_PublishesToKafkaAndMarksCompleted()
    {
        var aggregateId = Guid.NewGuid();
        var id = await InsertOutboxRowAsync(
            EventTypeNames.StockToggledStatus,
            JsonSerializer.Serialize(new AppEvents.StockToggledStatusEvent(aggregateId, DateTimeOffset.UtcNow)));

        await EnsureTopicsExistAsync(TopicNames.StockStatusToggled);
        await _service.StartAsync(CancellationToken.None);

        var result = Consume<StockToggledStatusEvent>(TopicNames.StockStatusToggled);
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

        var result = ConsumeJson<OutboxData>(_dlqOptions.UnknownTopic);
        Assert.Equal(id, result.Message.Value.Id);

        var status = await WaitForStatusAsync(id, "COMPLETED");
        Assert.Equal("COMPLETED", status);
    }

    [Fact]
    public async Task ExecuteAsync_MalformedPayloadForKnownEventType_IsNotMarkedCompleted()
    {
        var id = await InsertOutboxRowAsync(EventTypeNames.StockToggledStatus, "not-valid-json");

        await _service.StartAsync(CancellationToken.None);

        await Task.Delay(TimeSpan.FromSeconds(3));
        var status = await _dbContext.Connection.QuerySingleAsync<string>(
            "SELECT status FROM outbox WHERE id = @Id", new { Id = id });

        Assert.NotEqual("COMPLETED", status);
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

    private async Task<Guid> InsertOutboxRowAsync(string eventType, string payload)
    {
        var id = Guid.NewGuid();

        await _dbContext.Connection.ExecuteAsync("""
                                                 INSERT INTO outbox (id, aggregate_id, payload, event_type, status)
                                                 VALUES (@Id, @AggregateId, @Payload, @EventType, 'PENDING')
                                                 """, new
        {
            Id = id,
            AggregateId = Guid.NewGuid(),
            Payload = payload,
            EventType = eventType
        });

        return id;
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

    private ConsumeResult<string, TValue> Consume<TValue>(string topic)
    {
        using var consumer = new ConsumerBuilder<string, TValue>(new ConsumerConfig
            {
                BootstrapServers = _kafkaFixture.BootstrapAddress,
                GroupId = Guid.NewGuid().ToString(),
                AutoOffsetReset = AutoOffsetReset.Earliest
            })
            .SetValueDeserializer(new ProtobufNetDeserializer<TValue>())
            .Build();

        consumer.Subscribe(topic);
        var result = consumer.Consume(TimeSpan.FromSeconds(30));
        Assert.NotNull(result);
        consumer.Close();
        return result;
    }

    private ConsumeResult<string, TValue> ConsumeJson<TValue>(string topic)
    {
        using var consumer = new ConsumerBuilder<string, TValue>(new ConsumerConfig
            {
                BootstrapServers = _kafkaFixture.BootstrapAddress,
                GroupId = Guid.NewGuid().ToString(),
                AutoOffsetReset = AutoOffsetReset.Earliest
            })
            .SetValueDeserializer(new KafkaJsonDeserializer<TValue>())
            .Build();

        consumer.Subscribe(topic);
        var result = consumer.Consume(TimeSpan.FromSeconds(30));
        Assert.NotNull(result);
        consumer.Close();
        return result;
    }
}