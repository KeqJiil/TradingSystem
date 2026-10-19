using Dapper;
using MediatR;
using Messaging;
using Messaging.Serializers;
using Messaging.Testing;
using Microsoft.Extensions.DependencyInjection;
using Stock.Application.Commands.SetStockOpenToTrade;
using Stock.Application.Events;
using Stock.Infrastructure.Topics;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class SetStockOpenToTradeHandlerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncLifetime
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

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Handle_ExistingStock_SetsIsOpenToTrade(bool initial, bool requested)
    {
        var stockId = await SeedAsync(initial);

        Assert.True(await SendAsync(_provider, new SetStockOpenToTradeCommand(stockId, requested)));

        Assert.Equal(requested, await _db.Connection.ExecuteScalarAsync<bool>(
            "SELECT is_open_to_trade FROM stock_data WHERE id = @Id", new { Id = stockId }));
    }

    [Fact]
    public async Task Handle_TwoToggles_PublishesStatusToggledWithNewStateAndIncrementedVersions()
    {
        var stockId = await SeedAsync(true);

        await SendAsync(_provider, new SetStockOpenToTradeCommand(stockId, false));
        await SendAsync(_provider, new SetStockOpenToTradeCommand(stockId, true));

        var published = await Published.OfTypeAsync<StockToggledStatusEvent>(stockId.ToString());
        Assert.All(published, p => Assert.Equal(StockTopic.StockStatusToggled.Name(), p.Topic));
        Assert.Equal([1L, 2L], published.Select(p => p.Message.StatusVersion));
        Assert.Equal([false, true], published.Select(p => p.Message.IsOpenToTrade));
    }

    [Fact]
    public async Task Handle_UnknownStock_ReturnsFalseAndPublishesNothing()
    {
        var stockId = Guid.NewGuid();

        Assert.False(await SendAsync(_provider, new SetStockOpenToTradeCommand(stockId, true)));

        Assert.Empty(await Published.OfTypeAsync<StockToggledStatusEvent>(stockId.ToString()));
    }

    [Fact]
    public async Task Handle_PublishFails_KeepsOldStateAndVersion()
    {
        var stockId = await SeedAsync(true);
        await using var failing = BuildProvider(b => b.AddPublishMiddleware<FailingPublishMiddleware>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SendAsync(failing, new SetStockOpenToTradeCommand(stockId, false)));

        var stock = await _db.Connection.QuerySingleAsync(
            "SELECT is_open_to_trade, metadata_version FROM stock_data WHERE id = @Id", new { Id = stockId });
        Assert.True((bool)stock.is_open_to_trade);
        Assert.Equal(0L, (long)stock.metadata_version);
    }

    private ServiceProvider BuildProvider(Action<MessagingBuilder>? configure = null)
    {
        return StockServices.Build(fixture.ConnectionString, b =>
        {
            b.AddMessage<StockToggledStatusEvent>(StockTopic.StockStatusToggled.Name(), new JsonDefaultSerializer());
            configure?.Invoke(b);
        });
    }

    private static async Task<bool> SendAsync(IServiceProvider provider, SetStockOpenToTradeCommand command)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
    }

    private async Task<Guid> SeedAsync(bool isOpenToTrade)
    {
        var id = Guid.NewGuid();
        await _db.Connection.ExecuteAsync(
            "INSERT INTO stock_data (id, name, is_open_to_trade, trading_start_time, trading_end_time, currency) VALUES (@Id, 'StockName', @IsOpenToTrade, @OpenTime, @CloseTime, 'USD')",
            new { Id = id, IsOpenToTrade = isOpenToTrade, OpenTime = TimeSpan.FromHours(9), CloseTime = TimeSpan.FromHours(17) });
        return id;
    }
}
