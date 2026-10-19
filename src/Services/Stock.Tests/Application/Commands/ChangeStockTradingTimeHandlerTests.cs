using Dapper;
using MediatR;
using Messaging;
using Messaging.Testing;
using Microsoft.Extensions.DependencyInjection;
using Stock.Application.Commands.ChangeStockTradingTime;
using Stock.Application.Events;
using Stock.Infrastructure.Topics;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class ChangeStockTradingTimeHandlerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncLifetime
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
    public async Task Handle_ExistingStock_ChangesTradingTime()
    {
        var stockId = await SeedAsync();

        Assert.True(await SendAsync(_provider, NewCommand(stockId)));

        var stock = await _db.Connection.QuerySingleAsync(
            "SELECT trading_start_time, trading_end_time FROM stock_data WHERE id = @Id", new { Id = stockId });
        Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromTimeSpan((TimeSpan)stock.trading_start_time));
        Assert.Equal(new TimeOnly(17, 0), TimeOnly.FromTimeSpan((TimeSpan)stock.trading_end_time));
    }

    [Fact]
    public async Task Handle_TwoChanges_PublishesTimeChangedWithIncrementedVersions()
    {
        var stockId = await SeedAsync();

        await SendAsync(_provider, NewCommand(stockId));
        await SendAsync(_provider, new ChangeStockTradingTimeCommand(stockId, new TimeOnly(10, 0), new TimeOnly(18, 0)));

        var published = await Published.OfTypeAsync<TimeChangedEvent>(stockId.ToString());
        Assert.All(published, p => Assert.Equal(StockTopic.StockTradingTimeChanged.Name(), p.Topic));
        Assert.Equal([1L, 2L], published.Select(p => p.Message.Version));
        Assert.Equal([new TimeOnly(9, 0), new TimeOnly(10, 0)], published.Select(p => p.Message.TradingStartTime));
        Assert.Equal([new TimeOnly(17, 0), new TimeOnly(18, 0)], published.Select(p => p.Message.TradingCloseTime));
    }

    [Fact]
    public async Task Handle_UnknownStock_ReturnsFalseAndPublishesNothing()
    {
        var stockId = Guid.NewGuid();

        Assert.False(await SendAsync(_provider, NewCommand(stockId)));

        Assert.Empty(await Published.OfTypeAsync<TimeChangedEvent>(stockId.ToString()));
    }

    [Fact]
    public async Task Handle_PublishFails_KeepsOldTradingTimeAndVersion()
    {
        var stockId = await SeedAsync();
        await using var failing = BuildProvider(b => b.AddPublishMiddleware<FailingPublishMiddleware>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => SendAsync(failing, NewCommand(stockId)));

        var stock = await _db.Connection.QuerySingleAsync(
            "SELECT trading_start_time, metadata_version FROM stock_data WHERE id = @Id", new { Id = stockId });
        Assert.Equal(new TimeOnly(1, 0), TimeOnly.FromTimeSpan((TimeSpan)stock.trading_start_time));
        Assert.Equal(0L, (long)stock.metadata_version);
    }

    private static ChangeStockTradingTimeCommand NewCommand(Guid stockId)
    {
        return new ChangeStockTradingTimeCommand(stockId, new TimeOnly(9, 0), new TimeOnly(17, 0));
    }

    private ServiceProvider BuildProvider(Action<IMessagingBuilder>? configure = null)
    {
        return StockServices.Build(fixture.ConnectionString, configure);
    }

    private static async Task<bool> SendAsync(IServiceProvider provider, ChangeStockTradingTimeCommand command)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
    }

    private async Task<Guid> SeedAsync()
    {
        var id = Guid.NewGuid();
        await _db.Connection.ExecuteAsync(
            "INSERT INTO stock_data (id, name, is_open_to_trade, trading_start_time, trading_end_time, currency) VALUES (@Id, 'StockName', 1, @OpenTime, @CloseTime, 'USD')",
            new { Id = id, OpenTime = TimeSpan.FromHours(1), CloseTime = TimeSpan.FromHours(23) });
        return id;
    }
}
