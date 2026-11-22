using Order.Domain;
using Xunit;

namespace Order.Tests.Domain.Aggregates;

public class StockUpdateTests
{
    private static readonly TimeOnly NewOpen = new(10, 0);
    private static readonly TimeOnly NewClose = new(15, 0);

    [Fact]
    public void Rename_NewerVersion_AppliesNameAndVersion()
    {
        var stock = TestStocks.Create();

        var result = stock.Rename("MSFT", 3);

        Assert.True(result.IsSuccess);
        Assert.Equal("MSFT", stock.Name);
        Assert.Equal(3, stock.NameVersion);
    }

    [Fact]
    public void Rename_EqualVersion_IsIgnored()
    {
        var stock = TestStocks.Create();
        stock.Rename("MSFT", 3);

        var result = stock.Rename("GOOG", 3);

        Assert.True(result.IsSuccess);
        Assert.Equal("MSFT", stock.Name);
        Assert.Equal(3, stock.NameVersion);
    }

    [Fact]
    public void Rename_OlderVersion_IsIgnored()
    {
        var stock = TestStocks.Create();
        stock.Rename("MSFT", 3);

        var result = stock.Rename("GOOG", 2);

        Assert.True(result.IsSuccess);
        Assert.Equal("MSFT", stock.Name);
        Assert.Equal(3, stock.NameVersion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_BlankName_ReturnsValidation(string name)
    {
        var stock = TestStocks.Create();

        var result = stock.Rename(name, 1);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("AAPL", stock.Name);
        Assert.Equal(0, stock.NameVersion);
    }

    [Fact]
    public void SetStatus_NewerVersion_AppliesStatusAndVersion()
    {
        var stock = TestStocks.Create();

        var result = stock.SetStatus(false, 3);

        Assert.True(result.IsSuccess);
        Assert.False(stock.IsOpenToTrade);
        Assert.Equal(3, stock.StatusVersion);
    }

    [Fact]
    public void SetStatus_EqualVersion_IsIgnored()
    {
        var stock = TestStocks.Create();
        stock.SetStatus(false, 3);

        var result = stock.SetStatus(true, 3);

        Assert.True(result.IsSuccess);
        Assert.False(stock.IsOpenToTrade);
        Assert.Equal(3, stock.StatusVersion);
    }

    [Fact]
    public void SetStatus_OlderVersion_IsIgnored()
    {
        var stock = TestStocks.Create();
        stock.SetStatus(false, 3);

        var result = stock.SetStatus(true, 2);

        Assert.True(result.IsSuccess);
        Assert.False(stock.IsOpenToTrade);
        Assert.Equal(3, stock.StatusVersion);
    }

    [Fact]
    public void SetTradingTime_NewerVersion_AppliesTimesAndVersion()
    {
        var stock = TestStocks.Create();

        var result = stock.SetTradingTime(NewOpen, NewClose, 3);

        Assert.True(result.IsSuccess);
        Assert.Equal(NewOpen, stock.TradingStart);
        Assert.Equal(NewClose, stock.TradingEnd);
        Assert.Equal(3, stock.TradingTimeVersion);
    }

    [Fact]
    public void SetTradingTime_EqualVersion_IsIgnored()
    {
        var stock = TestStocks.Create();
        stock.SetTradingTime(NewOpen, NewClose, 3);

        var result = stock.SetTradingTime(TestStocks.Open, TestStocks.Close, 3);

        Assert.True(result.IsSuccess);
        Assert.Equal(NewOpen, stock.TradingStart);
        Assert.Equal(NewClose, stock.TradingEnd);
        Assert.Equal(3, stock.TradingTimeVersion);
    }

    [Fact]
    public void SetTradingTime_OlderVersion_IsIgnored()
    {
        var stock = TestStocks.Create();
        stock.SetTradingTime(NewOpen, NewClose, 3);

        var result = stock.SetTradingTime(TestStocks.Open, TestStocks.Close, 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(NewOpen, stock.TradingStart);
        Assert.Equal(NewClose, stock.TradingEnd);
        Assert.Equal(3, stock.TradingTimeVersion);
    }
}
