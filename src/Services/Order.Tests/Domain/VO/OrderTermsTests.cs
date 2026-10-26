using Order.Domain;
using Order.Domain.Aggregates;
using Order.Domain.VO;
using Xunit;

namespace Order.Tests.Domain.VO;

public class OrderTermsTests
{
    private static Price SomePrice => Price.Create(10m).Value;

    [Theory]
    [InlineData((TimeInForce)0)]
    [InlineData((TimeInForce)99)]
    public void Create_InvalidTimeInForce_ReturnsValidationFailure(TimeInForce timeInForce)
    {
        var result = OrderTerms.Create(timeInForce, OrderType.Limit, SomePrice);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Invalid order terms.", result.Error.Message);
    }

    [Theory]
    [InlineData((OrderType)0)]
    [InlineData((OrderType)99)]
    public void Create_InvalidOrderType_ReturnsValidationFailure(OrderType type)
    {
        var result = OrderTerms.Create(TimeInForce.Day, type, SomePrice);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Invalid order terms.", result.Error.Message);
    }

    [Theory]
    [InlineData(TimeInForce.Ioc)]
    [InlineData(TimeInForce.Fok)]
    public void Create_MarketWithLimitPrice_ReturnsValidationFailure(TimeInForce timeInForce)
    {
        var result = OrderTerms.Create(timeInForce, OrderType.Market, SomePrice);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Limit price should not be set for market orders.", result.Error.Message);
    }

    [Theory]
    [InlineData(TimeInForce.Day)]
    [InlineData(TimeInForce.Gtc)]
    [InlineData(TimeInForce.Ioc)]
    [InlineData(TimeInForce.Fok)]
    public void Create_LimitWithoutLimitPrice_ReturnsValidationFailure(TimeInForce timeInForce)
    {
        var result = OrderTerms.Create(timeInForce, OrderType.Limit);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Limit price is required for limit orders.", result.Error.Message);
    }

    [Theory]
    [InlineData(TimeInForce.Day)]
    [InlineData(TimeInForce.Gtc)]
    public void Create_MarketWithDayOrGtc_ReturnsValidationFailure(TimeInForce timeInForce)
    {
        var result = OrderTerms.Create(timeInForce, OrderType.Market);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Market orders cannot have TimeInForce set to Day or Gtc.", result.Error.Message);
    }

    [Theory]
    [InlineData(TimeInForce.Day)]
    [InlineData(TimeInForce.Gtc)]
    [InlineData(TimeInForce.Ioc)]
    [InlineData(TimeInForce.Fok)]
    public void Create_LimitWithLimitPrice_ReturnsOrderTerms(TimeInForce timeInForce)
    {
        var price = SomePrice;

        var result = OrderTerms.Create(timeInForce, OrderType.Limit, price);

        Assert.True(result.IsSuccess);
        Assert.Equal(timeInForce, result.Value.TimeInForce);
        Assert.Equal(OrderType.Limit, result.Value.Type);
        Assert.Equal(price, result.Value.LimitPrice);
    }

    [Theory]
    [InlineData(TimeInForce.Ioc)]
    [InlineData(TimeInForce.Fok)]
    public void Create_MarketWithIocOrFokWithoutLimitPrice_ReturnsOrderTerms(TimeInForce timeInForce)
    {
        var result = OrderTerms.Create(timeInForce, OrderType.Market);

        Assert.True(result.IsSuccess);
        Assert.Equal(timeInForce, result.Value.TimeInForce);
        Assert.Equal(OrderType.Market, result.Value.Type);
        Assert.Null(result.Value.LimitPrice);
    }
}
