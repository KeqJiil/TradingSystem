using Order.Domain;
using Order.Domain.Aggregates;
using Order.Domain.Specifications;
using Order.Domain.VO;
using Xunit;

namespace Order.Tests.Domain.Specifications;

public class ApplyFillSpecificationTests
{
    private static Price P(decimal value)
    {
        return Price.Create(value).Value;
    }

    private static Quantity Q(int value)
    {
        return Quantity.Create(value).Value;
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.PartiallyFilled)]
    [InlineData(OrderStatus.PendingCancel)]
    public void Validate_NotApproved_ReturnsViolation(OrderStatus status)
    {
        var result = ApplyFillSpecification.Validate(OrderSide.Buy, P(10m), null, Q(1), 10, status, false);

        AssertViolation(result, "Order must be approved before it can be filled.");
    }

    [Theory]
    [InlineData(10.01)]
    [InlineData(15)]
    public void Validate_BuyFillPriceAboveLimit_ReturnsViolation(double fillPrice)
    {
        var result = ApplyFillSpecification.Validate(OrderSide.Buy, P((decimal)fillPrice), P(10m), Q(1), 10,
            OrderStatus.New, true);

        AssertViolation(result, "Fill price exceeds limit price.");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(9.99)]
    [InlineData(1)]
    public void Validate_BuyFillPriceAtOrBelowLimit_ReturnsSuccess(double fillPrice)
    {
        var result = ApplyFillSpecification.Validate(OrderSide.Buy, P((decimal)fillPrice), P(10m), Q(1), 10,
            OrderStatus.New, true);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(9.99)]
    [InlineData(1)]
    public void Validate_SellFillPriceBelowLimit_ReturnsViolation(double fillPrice)
    {
        var result = ApplyFillSpecification.Validate(OrderSide.Sell, P((decimal)fillPrice), P(10m), Q(1), 10,
            OrderStatus.New, true);

        AssertViolation(result, "Fill price is below limit price.");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(10.01)]
    [InlineData(15)]
    public void Validate_SellFillPriceAtOrAboveLimit_ReturnsSuccess(double fillPrice)
    {
        var result = ApplyFillSpecification.Validate(OrderSide.Sell, P((decimal)fillPrice), P(10m), Q(1), 10,
            OrderStatus.New, true);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(OrderSide.Buy, 0.01)]
    [InlineData(OrderSide.Buy, 99999)]
    [InlineData(OrderSide.Sell, 0.01)]
    [InlineData(OrderSide.Sell, 99999)]
    public void Validate_MarketOrderAnyFillPrice_ReturnsSuccess(OrderSide side, double fillPrice)
    {
        var result = ApplyFillSpecification.Validate(side, P((decimal)fillPrice), null, Q(1), 10,
            OrderStatus.New, true);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(11, 10)]
    [InlineData(2, 1)]
    [InlineData(1, 0)]
    public void Validate_QuantityExceedsRemaining_ReturnsViolation(int quantity, int remaining)
    {
        var result = ApplyFillSpecification.Validate(OrderSide.Buy, P(10m), null, Q(quantity), remaining,
            OrderStatus.PartiallyFilled, true);

        AssertViolation(result, "Fill quantity exceeds remaining order quantity.");
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(1, 10)]
    [InlineData(1, 1)]
    public void Validate_QuantityWithinRemaining_ReturnsSuccess(int quantity, int remaining)
    {
        var result = ApplyFillSpecification.Validate(OrderSide.Buy, P(10m), null, Q(quantity), remaining,
            OrderStatus.PartiallyFilled, true);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(OrderStatus.PendingNew)]
    [InlineData(OrderStatus.Rejected)]
    [InlineData(OrderStatus.Filled)]
    [InlineData(OrderStatus.Canceled)]
    [InlineData(OrderStatus.Expired)]
    public void Validate_NotFillableStatus_ReturnsViolation(OrderStatus status)
    {
        var result = ApplyFillSpecification.Validate(OrderSide.Buy, P(10m), null, Q(1), 10, status, true);

        AssertViolation(result, "Order can only be filled from New or PartiallyFilled or PendingCancel status.");
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.PartiallyFilled)]
    [InlineData(OrderStatus.PendingCancel)]
    public void Validate_FillableStatus_ReturnsSuccess(OrderStatus status)
    {
        var result = ApplyFillSpecification.Validate(OrderSide.Buy, P(10m), null, Q(1), 10, status, true);

        Assert.True(result.IsSuccess);
    }

    private static void AssertViolation(Result result, string message)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Violation, result.Error.Type);
        Assert.Equal(message, result.Error.Message);
    }
}