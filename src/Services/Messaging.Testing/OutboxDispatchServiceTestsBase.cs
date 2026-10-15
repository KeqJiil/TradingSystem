using System.Text;
using Messaging.Abstractions;
using Messaging.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Messaging.Testing;

public abstract class OutboxDispatchServiceTestsBase : IAsyncLifetime
{
    private readonly MssqlFixtureBase _db;
    private readonly KafkaFixture _kafkaFixture;
    private readonly IOutboxTable _table;
    private readonly KafkaReader _kafka;
    private readonly CapturingLogger<OutboxDispatchService> _logger = new();
    private readonly List<(ServiceProvider Provider, OutboxDispatchService Relay)> _relays = [];

    protected OutboxDispatchServiceTestsBase(MssqlFixtureBase db, KafkaFixture kafka)
    {
        _db = db;
        _kafkaFixture = kafka;
        _table = CreateTable(db.ConnectionString);
        _kafka = new KafkaReader(kafka.BootstrapAddress);
    }

    protected abstract IOutboxTable CreateTable(string connectionString);

    protected abstract void AddPersistence(IServiceCollection services, string connectionString);

    protected abstract void AddOutbox(IMessagingBuilder builder, Action<OutboxRelayOptions> configure);

    public async Task InitializeAsync()
    {
        await _table.ClearAsync();
    }

    public async Task DisposeAsync()
    {
        foreach (var (provider, relay) in _relays)
        {
            await relay.StopAsync(CancellationToken.None);
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task Relay_PendingRows_PublishesThemToKafkaAndMarksCompleted()
    {
        var topic = await _kafka.CreateTopicAsync();
        for (var i = 0; i < 3; i++)
            await _table.SeedAsync(new OutboxSeed { Topic = topic, Key = $"key-{i}", Payload = Bytes($"m-{i}") });

        await StartRelayAsync(_kafkaFixture.BootstrapAddress);

        var messages = await _kafka.ReadAsync(topic, 3);
        Assert.Equal(["m-0", "m-1", "m-2"], messages.Select(m => Encoding.UTF8.GetString(m.Value)).Order());
        await Wait.UntilAsync(async () => (await _table.GetAllAsync()).All(r => r.Status == "COMPLETED"),
            "all rows completed");
    }

    [Fact]
    public async Task Relay_Row_KafkaMessageCarriesKeyPayloadAndHeaders()
    {
        var topic = await _kafka.CreateTopicAsync();
        var seed = new OutboxSeed
        {
            Topic = topic, Key = "order-1", MessageType = "OrderPlaced", Payload = Bytes("payload"),
            Headers = new Dictionary<string, string>
            {
                [MessagingHeaders.CorrelationId] = "c-1",
                ["custom"] = "value"
            }
        };
        await _table.SeedAsync(seed);

        await StartRelayAsync(_kafkaFixture.BootstrapAddress);

        var message = Assert.Single(await _kafka.ReadAsync(topic, 1));
        Assert.Equal("order-1", message.Key);
        Assert.Equal("payload", Encoding.UTF8.GetString(message.Value));
        Assert.Equal("c-1", message.Headers[MessagingHeaders.CorrelationId]);
        Assert.Equal("value", message.Headers["custom"]);
        Assert.Equal("OrderPlaced", message.Headers[MessagingHeaders.EventType]);
        Assert.Equal(seed.MessageId, message.Headers[MessagingHeaders.MessageId]);
    }

    [Fact]
    public async Task Relay_BrokerUnreachable_LeavesRowProcessing()
    {
        var id = await _table.SeedAsync(new OutboxSeed());

        await StartRelayAsync("localhost:1", messageTimeoutMs: 2_000);

        await Wait.UntilAsync(() => Task.FromResult(HasLog(LogLevel.Warning, "Failed to publish")),
            "publish failure logged");
        var row = await _table.GetAsync(id);
        Assert.Equal("PROCESSING", row!.Status);
        Assert.Equal(1, row.Attempts);
    }

    [Fact]
    public async Task Relay_PayloadTooLarge_MarksRowDeadAndPublishesNextMessageOfSameKey()
    {
        var topic = await _kafka.CreateTopicAsync();
        var poison = await _table.SeedAsync(new OutboxSeed { Topic = topic, Key = "a", Payload = new byte[2_000_000] });
        var next = await _table.SeedAsync(new OutboxSeed { Topic = topic, Key = "a", Payload = Bytes("next") });

        await StartRelayAsync(_kafkaFixture.BootstrapAddress);

        await Wait.UntilAsync(async () => (await _table.GetAsync(next))!.Status == "COMPLETED", "next completed");
        Assert.Equal("DEAD", (await _table.GetAsync(poison))!.Status);
        Assert.True(HasLog(LogLevel.Critical, "marked as dead"));
        Assert.Equal("next", Encoding.UTF8.GetString(Assert.Single(await _kafka.ReadAsync(topic, 1)).Value));
    }

    [Fact]
    public async Task Relay_AttemptsExhausted_MarksRowDeadAndPublishesNextMessageOfSameKey()
    {
        var topic = await _kafka.CreateTopicAsync();
        var failing = await _table.SeedAsync(new OutboxSeed { Topic = "invalid topic!", Key = "a" });
        var next = await _table.SeedAsync(new OutboxSeed { Topic = topic, Key = "a", Payload = Bytes("next") });

        await StartRelayAsync(_kafkaFixture.BootstrapAddress, messageTimeoutMs: 5_000, maxAttempts: 1);

        await Wait.UntilAsync(async () => (await _table.GetAsync(next))!.Status == "COMPLETED", "next completed");
        Assert.Equal("DEAD", (await _table.GetAsync(failing))!.Status);
        Assert.True(HasLog(LogLevel.Critical, "exhausted 1 attempts"));
        Assert.Equal("next", Encoding.UTF8.GetString(Assert.Single(await _kafka.ReadAsync(topic, 1)).Value));
    }

    [Fact]
    public async Task Relay_KeyFails_HoldsRestOfThatKeyAndCompletesOtherKeys()
    {
        var topic = await _kafka.CreateTopicAsync();
        var failing = await _table.SeedAsync(new OutboxSeed { Topic = "invalid topic!", Key = "a" });
        var held = await _table.SeedAsync(new OutboxSeed { Topic = topic, Key = "a", Payload = Bytes("held") });
        var other = await _table.SeedAsync(new OutboxSeed { Topic = topic, Key = "b", Payload = Bytes("other") });

        await StartRelayAsync(_kafkaFixture.BootstrapAddress, messageTimeoutMs: 5_000);

        await Wait.UntilAsync(async () => (await _table.GetAsync(other))!.Status == "COMPLETED", "other key completed");
        var failingRow = await _table.GetAsync(failing);
        var heldRow = await _table.GetAsync(held);
        Assert.Equal("PROCESSING", failingRow!.Status);
        Assert.Equal("PROCESSING", heldRow!.Status);
        var published = await _kafka.ReadAsync(topic, 2, TimeSpan.FromSeconds(5));
        Assert.Equal(["other"], published.Select(m => Encoding.UTF8.GetString(m.Value)));
    }

    [Fact]
    public async Task Relay_DatabaseUnavailable_KeepsRunningAndRecoversWhenDatabaseAppears()
    {
        var connectionString = new SqlConnectionStringBuilder(_db.NewDatabaseConnectionString()) { Pooling = false }
            .ConnectionString;
        var topic = await _kafka.CreateTopicAsync();

        await StartRelayAsync(_kafkaFixture.BootstrapAddress, connectionString);
        await Wait.UntilAsync(() => Task.FromResult(HasLog(LogLevel.Error, "unexpected error")),
            "dispatch error logged");

        await _db.CreateSchemaAsync(connectionString);
        var table = CreateTable(connectionString);
        var id = await table.SeedAsync(new OutboxSeed { Topic = topic, Payload = Bytes("late") });

        Assert.Equal("late", Encoding.UTF8.GetString(Assert.Single(await _kafka.ReadAsync(topic, 1)).Value));
        await Wait.UntilAsync(async () => (await table.GetAsync(id))!.Status == "COMPLETED", "row completed");
    }

    private async Task StartRelayAsync(string bootstrapServers, string? connectionString = null,
        int messageTimeoutMs = 10_000, int maxAttempts = 3)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILogger<OutboxDispatchService>>(_logger);
        AddPersistence(services, connectionString ?? _db.ConnectionString);
        services.AddMessaging(b => AddOutbox(b, o =>
        {
            o.PollInterval = TimeSpan.FromMilliseconds(200);
            o.LeaseMinutes = 1;
            o.MaxAttempts = maxAttempts;
        }), o =>
        {
            o.BootstrapServers = bootstrapServers;
            o.MessageTimeoutMs = messageTimeoutMs;
            o.EnableTopicRegistration = false;
        });

        var provider = services.BuildServiceProvider();
        var relay = provider.GetServices<IHostedService>().OfType<OutboxDispatchService>().Single();
        _relays.Add((provider, relay));
        await relay.StartAsync(CancellationToken.None);
    }

    private bool HasLog(LogLevel level, string text)
    {
        lock (_logger.Entries) return _logger.Entries.Any(e => e.Level == level && e.Message.Contains(text));
    }

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
}
