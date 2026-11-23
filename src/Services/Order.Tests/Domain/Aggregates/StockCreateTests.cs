using Order.Domain;
using Order.Domain.Aggregates;
using Xunit;

namespace Order.Tests.Domain.Aggregates;

public class StockCreateTests
{
    private static readonly TimeOnly Open = new(9, 30);
    private static readonly TimeOnly Close = new(16, 0);

    [Fact]
    public void Create_ValidArguments_SetsFieldsAndZeroVersions()
    {
        var id = Guid.CreateVersion7();

        var result = Stock.Create(id, "AAPL", true, Open, Close);

        Assert.True(result.IsSuccess);
        var stock = result.Value;
        Assert.Equal(id, stock.Id);
        Assert.Equal("AAPL", stock.Name);
        Assert.True(stock.IsOpenToTrade);
        Assert.Equal(Open, stock.TradingStart);
        Assert.Equal(Close, stock.TradingEnd);
        Assert.Equal(0, stock.NameVersion);
        Assert.Equal(0, stock.StatusVersion);
        Assert.Equal(0, stock.TradingTimeVersion);
    }

    [Fact]
    public void Create_EmptyId_ReturnsValidation()
    {
        var result = Stock.Create(Guid.Empty, "AAPL", true, Open, Close);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_ReturnsValidation(string name)
    {
        var result = Stock.Create(Guid.CreateVersion7(), name, true, Open, Close);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }
}
