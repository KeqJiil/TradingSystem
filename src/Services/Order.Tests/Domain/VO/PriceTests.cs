using Order.Domain;
using Order.Domain.VO;
using Xunit;

namespace Order.Tests.Domain.VO;

public class PriceTests
{
    public static TheoryData<decimal> NonPositive => [-0.01m, -100m, 0m];
    public static TheoryData<decimal> TooPrecise => [1.001m, 0.005m, 10.123m];
    public static TheoryData<decimal> Valid => [0.01m, 1m, 1.50m, 100.5m, 99999.99m];

    [Theory]
    [MemberData(nameof(NonPositive))]
    public void Create_NonPositiveValue_ReturnsValidationFailure(decimal value)
    {
        var result = Price.Create(value);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Price must be greater than zero.", result.Error.Message);
    }

    [Theory]
    [MemberData(nameof(TooPrecise))]
    public void Create_MoreThanTwoDecimalPlaces_ReturnsValidationFailure(decimal value)
    {
        var result = Price.Create(value);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Price must be a multiple of 0.01.", result.Error.Message);
    }

    [Theory]
    [MemberData(nameof(Valid))]
    public void Create_ValidValue_ReturnsPrice(decimal value)
    {
        var result = Price.Create(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value.Value);
    }
}
