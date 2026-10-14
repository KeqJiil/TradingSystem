using System.Text.Json;
using Messaging.Abstractions;
using Messaging.Dapper.Outbox;
using Messaging.Dapper.Tests.Integration.Infrastructure;
using Messaging.Dapper.Transactions;
using Messaging.Serializers;
using Microsoft.Data.SqlClient;

namespace Messaging.Dapper.Tests.Integration.Outbox;

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
    private IUnitOfWorkDecorator UnitOfWork => _scope.ServiceProvider.GetRequiredService<IUnitOfWorkDecorator>();

    public async Task InitializeAsync()
    {
        await _table.ClearAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUnitOfWork(_ => new SqlConnection(db.ConnectionString));
        services.AddMessaging(b =>
        {
            b.AddOutbox();
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
    public async Task Send_WithoutTransaction_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Sut.SendAsync("orders", "order-1", NewId(), new OrderPlaced("order-1", 10m),
                new Dictionary<string, string>(), CancellationToken.None));

        Assert.Empty(await _table.GetAllAsync());
    }

    [Fact]
    public async Task Send_SerializationFails_ReturnsFailureAndWritesNothing()
    {
        PublishOutcome outcome = default;

        await UnitOfWork.ExecuteAsync(async () =>
        {
            outcome = await Sut.SendAsync("broken", "key", NewId(), new Unserializable(),
                new Dictionary<string, string>(), CancellationToken.None);
        }, CancellationToken.None);

        Assert.False(outcome.IsSuccessful);
        Assert.NotNull(outcome.ErrorMessage);
        Assert.Empty(await _table.GetAllAsync());
    }

    [Fact]
    public async Task Send_InCommittedTransaction_WritesPendingRowAndReturnsSuccess()
    {
        var messageId = NewId();
        var message = new OrderPlaced("order-1", 10m);
        var headers = new Dictionary<string, string> { ["correlation-id"] = "c-1" };
        PublishOutcome outcome = default;

        await UnitOfWork.ExecuteAsync(async () =>
        {
            outcome = await Sut.SendAsync("orders", "order-1", messageId, message, headers, CancellationToken.None);
        }, CancellationToken.None);

        Assert.True(outcome.IsSuccessful);
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
    public async Task Send_CallerTransactionRolledBack_LeavesNoRow()
    {
        await Assert.ThrowsAsync<ApplicationException>(() => UnitOfWork.ExecuteAsync(async () =>
        {
            var outcome = await Sut.SendAsync("orders", "order-1", NewId(), new OrderPlaced("order-1", 10m),
                new Dictionary<string, string>(), CancellationToken.None);
            Assert.True(outcome.IsSuccessful);
            throw new ApplicationException("caller failed");
        }, CancellationToken.None));

        Assert.Empty(await _table.GetAllAsync());
    }

    private static string NewId() => Guid.NewGuid().ToString();
}
