using Order.Application.UseCases.ChangeStockTradingTime;
using Order.Domain;
using Order.Tests.Domain.Aggregates;
using Order.Tests.Infrastructure;
using Xunit;

namespace Order.Tests.Application.UseCases;

public class ChangeStockTradingTimeCommandHandlerTests(MssqlFixture fixture)
    : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private static readonly TimeOnly NewOpen = new(10, 0);
    private static readonly TimeOnly NewClose = new(15, 0);

    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task Handle_UnknownStock_ReturnsNotFound()
    {
        var result = await _scope.SendAsync(
            new ChangeStockTradingTimeCommand(Guid.CreateVersion7(), NewOpen, NewClose, 1));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task Handle_NewerVersion_UpdatesRow()
    {
        var stock = await _scope.SeedStockAsync(TestStocks.Create());

        var result = await _scope.SendAsync(new ChangeStockTradingTimeCommand(stock.Id, NewOpen, NewClose, 2));

        Assert.True(result.IsSuccess);
        var row = await _scope.FindStockAsync(stock.Id);
        Assert.Equal(NewOpen, row!.TradingStart);
        Assert.Equal(NewClose, row.TradingEnd);
        Assert.Equal(2, row.TradingTimeVersion);
    }

    [Fact]
    public async Task Handle_StaleVersion_LeavesRowUnchanged()
    {
        var seeded = TestStocks.Create();
        seeded.SetTradingTime(NewOpen, NewClose, 5);
        await _scope.SeedStockAsync(seeded);

        var result = await _scope.SendAsync(
            new ChangeStockTradingTimeCommand(seeded.Id, TestStocks.Open, TestStocks.Close, 3));

        Assert.True(result.IsSuccess);
        var row = await _scope.FindStockAsync(seeded.Id);
        Assert.Equal(NewOpen, row!.TradingStart);
        Assert.Equal(NewClose, row.TradingEnd);
        Assert.Equal(5, row.TradingTimeVersion);
    }
}
