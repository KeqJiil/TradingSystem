using System.Text.Json;
using Messaging.Abstractions;
using Messaging.EFCore.Outbox;
using Messaging.EFCore.Tests.Infrastructure;
using Messaging.Serializers;
using Microsoft.EntityFrameworkCore;

namespace Messaging.EFCore.Tests.Outbox;

public sealed record OrderPlaced(string OrderId, decimal Amount);

public sealed class Unserializable
{
    public string Value => throw new InvalidOperationException("cannot serialize");
}

public class OutboxPublishTerminalTests(MssqlFixture db) : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly OutboxTable _table = new(db.ConnectionString);
    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;

    private IPublishTerminal Sut => _scope.ServiceProvider.GetRequiredService<IPublishTerminal>();
    private TestDbContext Context => _scope.ServiceProvider.GetRequiredService<TestDbContext>();

    public async Task InitializeAsync()
    {
        await _table.ClearAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestDbContext>(o => o.UseSqlServer(db.ConnectionString));
        services.AddMessaging(b =>
        {
            b.AddOutbox<TestDbContext>();
            b.AddMessage<OrderPlaced>("orders", new JsonDefaultSerializer());
            b.AddMessage<Unserializable>("broken", new JsonDefaultSerializer());
        }, o => o.BootstrapServers = "localhost:9092");
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateAsyncScope();
    }

    public async Task DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Send_SerializationFails_ReturnsFailureAndTracksNothing()
    {
        var outcome = await Sut.SendAsync("broken", "key", NewId(), new Unserializable(),
            new Dictionary<string, string>(), CancellationToken.None);
        await Context.SaveChangesAsync();

        Assert.False(outcome.IsSuccessful);
        Assert.NotNull(outcome.ErrorMessage);
        Assert.Empty(Context.ChangeTracker.Entries<OutboxEntity>());
        Assert.Empty(await _table.GetAllAsync());
    }

    [Fact]
    public async Task Send_Message_TrackedAndNotSavedUntilSaveChanges()
    {
        var messageId = NewId();
        var message = new OrderPlaced("order-1", 10m);
        var headers = new Dictionary<string, string> { ["correlation-id"] = "c-1" };

        await Sut.SendAsync("orders", "order-1", messageId, message, headers, CancellationToken.None);

        var tracked = Assert.Single(Context.ChangeTracker.Entries<OutboxEntity>());
        Assert.Equal(EntityState.Added, tracked.State);
        Assert.Empty(await _table.GetAllAsync());

        await Context.SaveChangesAsync();

        var row = Assert.Single(await _table.GetAllAsync());
        Assert.Equal(messageId, row.MessageId);
        Assert.Equal(nameof(OrderPlaced), row.MessageType);
        Assert.Equal("orders", row.Topic);
        Assert.Equal("order-1", row.MessageKey);
        Assert.Equal("PENDING", row.Status);
        Assert.Equal(message, JsonSerializer.Deserialize<OrderPlaced>(row.Payload));
        Assert.Equal(headers, JsonSerializer.Deserialize<Dictionary<string, string>>(row.Headers));
    }

    [Fact]
    public async Task Send_Message_ReturnsSuccess()
    {
        var outcome = await Sut.SendAsync("orders", "order-1", NewId(), new OrderPlaced("order-1", 10m),
            new Dictionary<string, string>(), CancellationToken.None);

        Assert.True(outcome.IsSuccessful);
        Assert.Null(outcome.ErrorMessage);
    }

    private static string NewId()
    {
        return Guid.NewGuid().ToString();
    }
}
