using Messaging.Abstractions;
using Messaging.Dapper.Outbox;
using Messaging.Dapper.Tests.Integration.Infrastructure;
using Messaging.Dapper.Tests.Integration.Outbox;
using Messaging.Dapper.Transactions;
using Messaging.Serializers;
using Microsoft.Data.SqlClient;

namespace Messaging.Dapper.Tests.Integration.Testing;

public sealed record ShipmentScheduled(string OrderId);

public class PublishedMessagesTests(MssqlFixture db) : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly OutboxTable _table = new(db.ConnectionString);
    private readonly List<ServiceProvider> _providers = [];

    public Task InitializeAsync() => _table.ClearAsync();

    public async Task DisposeAsync()
    {
        foreach (var provider in _providers) await provider.DisposeAsync();
    }

    [Fact]
    public async Task OfTypeAsync_CommittedPublish_ReturnsMessageWithTopicKeyAndHeaders()
    {
        var provider = BuildProvider();
        var headers = new Dictionary<string, string> { ["x-test"] = "1" };

        await PublishAsync(provider, new OrderPlaced("order-1", 10m), "order-1", headers);

        var published = Assert.Single(await Inspector(provider).OfTypeAsync<OrderPlaced>());
        Assert.Equal(new OrderPlaced("order-1", 10m), published.Message);
        Assert.Equal("orders", published.Topic);
        Assert.Equal("order-1", published.Key);
        Assert.Equal("1", published.Headers["x-test"]);
    }

    [Fact]
    public async Task OfTypeAsync_WithKey_ReturnsOnlyMessagesOfThatKey()
    {
        var provider = BuildProvider();

        await PublishAsync(provider, new OrderPlaced("order-1", 10m), "order-1");
        await PublishAsync(provider, new OrderPlaced("order-2", 20m), "order-2");

        var published = Assert.Single(await Inspector(provider).OfTypeAsync<OrderPlaced>("order-2"));
        Assert.Equal(new OrderPlaced("order-2", 20m), published.Message);
    }

    [Fact]
    public async Task OfTypeAsync_OtherMessageType_IsNotReturned()
    {
        var provider = BuildProvider();

        await PublishAsync(provider, new ShipmentScheduled("order-1"), "order-1");

        Assert.Empty(await Inspector(provider).OfTypeAsync<OrderPlaced>());
        Assert.Single(await Inspector(provider).OfTypeAsync<ShipmentScheduled>());
    }

    [Fact]
    public async Task OfTypeAsync_RolledBackPublish_ReturnsNothing()
    {
        var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        await Assert.ThrowsAsync<ApplicationException>(() =>
            scope.ServiceProvider.GetRequiredService<IUnitOfWorkDecorator>().ExecuteAsync(async () =>
            {
                await scope.ServiceProvider.GetRequiredService<IMessagePublisher>().PublishAsync(
                    new OrderPlaced("order-1", 10m), new PublishOptions("order-1"), CancellationToken.None);
                throw new ApplicationException("caller failed");
            }, CancellationToken.None));

        Assert.Empty(await Inspector(provider).OfTypeAsync<OrderPlaced>());
    }

    [Fact]
    public async Task FailingPublishMiddleware_InsideUnitOfWork_ThrowsAndPublishesNothing()
    {
        var provider = BuildProvider(b => b.AddPublishMiddleware<FailingPublishMiddleware>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PublishAsync(provider, new OrderPlaced("order-1", 10m), "order-1"));

        Assert.Empty(await Inspector(provider).OfTypeAsync<OrderPlaced>());
    }

    private ServiceProvider BuildProvider(Action<MessagingBuilder>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUnitOfWork(_ => new SqlConnection(db.ConnectionString));
        services.AddMessaging(b =>
        {
            b.AddOutbox();
            b.AddMessage<OrderPlaced>("orders", new JsonDefaultSerializer());
            b.AddMessage<ShipmentScheduled>("shipments", new JsonDefaultSerializer());
            configure?.Invoke(b);
        }, o => o.BootstrapServers = "localhost:9092");
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _providers.Add(provider);
        return provider;
    }

    private PublishedMessages Inspector(IServiceProvider provider) =>
        new(db.ConnectionString, provider.GetRequiredService<IMessageSerializerResolver>());

    private static async Task PublishAsync<TMessage>(IServiceProvider provider, TMessage message, string key,
        Dictionary<string, string>? headers = null)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWorkDecorator>().ExecuteAsync(() =>
            scope.ServiceProvider.GetRequiredService<IMessagePublisher>().PublishAsync(
                message, new PublishOptions(key, headers), CancellationToken.None), CancellationToken.None);
    }
}
