using Order.Application.UseCases.RegisterStock;
using Order.Tests.Infrastructure;
using Xunit;

namespace Order.Tests.Application.UseCases;

public class RegisterStockCommandHandlerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private static readonly TimeOnly Open = new(9, 30);
    private static readonly TimeOnly Close = new(16, 0);

    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task Handle_NewStock_PersistsRowWithZeroVersions()
    {
        var stockId = Guid.CreateVersion7();

        var result = await _scope.SendAsync(new RegisterStockCommand(stockId, "AAPL", true, Open, Close));

        Assert.True(result.IsSuccess);
        var stock = await _scope.FindStockAsync(stockId);
        Assert.NotNull(stock);
        Assert.Equal("AAPL", stock.Name);
        Assert.True(stock.IsOpenToTrade);
        Assert.Equal(Open, stock.TradingStart);
        Assert.Equal(Close, stock.TradingEnd);
        Assert.Equal(0, stock.NameVersion);
        Assert.Equal(0, stock.StatusVersion);
        Assert.Equal(0, stock.TradingTimeVersion);
    }

    [Fact]
    public async Task Handle_StockAlreadyExists_ReturnsSuccessAndKeepsFirstRow()
    {
        var stockId = Guid.CreateVersion7();
        await _scope.SendAsync(new RegisterStockCommand(stockId, "AAPL", true, Open, Close));

        var result = await _scope.SendAsync(
            new RegisterStockCommand(stockId, "MSFT", false, new TimeOnly(10, 0), new TimeOnly(15, 0)));

        Assert.True(result.IsSuccess);
        var stock = await _scope.FindStockAsync(stockId);
        Assert.Equal("AAPL", stock!.Name);
        Assert.True(stock.IsOpenToTrade);
        Assert.Equal(Open, stock.TradingStart);
        Assert.Equal(Close, stock.TradingEnd);
    }
}
