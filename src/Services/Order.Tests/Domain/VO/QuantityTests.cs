using Order.Domain;
using Order.Domain.VO;
using Xunit;

namespace Order.Tests.Domain.VO;

public class QuantityTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void Create_NegativeValue_ReturnsValidationFailure(int value)
    {
        var result = Quantity.Create(value);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Quantity must be greater than zero.", result.Error.Message);
    }

    [Fact]
    public void Create_ZeroValue_ReturnsValidationFailure()
    {
        var result = Quantity.Create(0);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Quantity must be greater than zero.", result.Error.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void Create_PositiveValue_ReturnsQuantity(int value)
    {
        var result = Quantity.Create(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value.Value);
    }
}
