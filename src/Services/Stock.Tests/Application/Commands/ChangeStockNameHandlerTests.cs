using Dapper;
using MediatR;
using Messaging;
using Messaging.Serializers;
using Messaging.Testing;
using Microsoft.Extensions.DependencyInjection;
using Stock.Application.Commands.ChangeStockName;
using Stock.Application.Events;
using Stock.Infrastructure.Topics;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class ChangeStockNameHandlerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly TestDbContext _db = new(fixture.ConnectionString);
    private ServiceProvider _provider = null!;

    private PublishedMessages Published => _provider.GetRequiredService<PublishedMessages>();

    public async Task InitializeAsync()
    {
        _provider = BuildProvider();
        await _db.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(_db);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ExistingStock_ChangesName()
    {
        var stockId = await SeedAsync();

        Assert.True(await SendAsync(_provider, new ChangeStockNameCommand(stockId, "NewName")));

        Assert.Equal("NewName", await _db.Connection.ExecuteScalarAsync<string>(
            "SELECT name FROM stock_data WHERE id = @Id", new { Id = stockId }));
    }

    [Fact]
    public async Task Handle_TwoChanges_PublishesNameChangedWithIncrementedVersions()
    {
        var stockId = await SeedAsync();

        await SendAsync(_provider, new ChangeStockNameCommand(stockId, "First"));
        await SendAsync(_provider, new ChangeStockNameCommand(stockId, "Second"));

        var published = await Published.OfTypeAsync<NameChangedEvent>(stockId.ToString());
        Assert.All(published, p => Assert.Equal(StockTopic.StockNameChanged.Name(), p.Topic));
        Assert.Equal([1L, 2L], published.Select(p => p.Message.Version));
        Assert.Equal(["First", "Second"], published.Select(p => p.Message.Name));
    }

    [Fact]
    public async Task Handle_UnknownStock_ReturnsFalseAndPublishesNothing()
    {
        var stockId = Guid.NewGuid();

        Assert.False(await SendAsync(_provider, new ChangeStockNameCommand(stockId, "NewName")));

        Assert.Empty(await Published.OfTypeAsync<NameChangedEvent>(stockId.ToString()));
    }

    [Fact]
    public async Task Handle_PublishFails_KeepsOldNameAndVersion()
    {
        var stockId = await SeedAsync();
        await using var failing = BuildProvider(b => b.AddPublishMiddleware<FailingPublishMiddleware>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SendAsync(failing, new ChangeStockNameCommand(stockId, "NewName")));

        var stock = await _db.Connection.QuerySingleAsync(
            "SELECT name, metadata_version FROM stock_data WHERE id = @Id", new { Id = stockId });
        Assert.Equal("OldName", stock.name);
        Assert.Equal(0L, (long)stock.metadata_version);
    }

    private ServiceProvider BuildProvider(Action<MessagingBuilder>? configure = null)
    {
        return StockServices.Build(fixture.ConnectionString, b =>
        {
            b.AddMessage<NameChangedEvent>(StockTopic.StockNameChanged.Name(), new JsonDefaultSerializer());
            configure?.Invoke(b);
        });
    }

    private static async Task<bool> SendAsync(IServiceProvider provider, ChangeStockNameCommand command)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
    }

    private async Task<Guid> SeedAsync()
    {
        var id = Guid.NewGuid();
        await _db.Connection.ExecuteAsync(
            "INSERT INTO stock_data (id, name, is_open_to_trade, trading_start_time, trading_end_time, currency) VALUES (@Id, 'OldName', 1, @OpenTime, @CloseTime, 'USD')",
            new { Id = id, OpenTime = TimeSpan.FromHours(9), CloseTime = TimeSpan.FromHours(17) });
        return id;
    }
}