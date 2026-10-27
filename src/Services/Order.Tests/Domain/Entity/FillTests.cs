using Order.Domain;
using Order.Domain.Entity;
using Order.Domain.VO;
using Xunit;

namespace Order.Tests.Domain.Entity;

public class FillTests
{
    [Fact]
    public void Create_ValidArguments_ReturnsFill()
    {
        var executionId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var quantity = Quantity.Create(10).Value;
        var price = Price.Create(12.34m).Value;
        var filledAt = DateTimeOffset.UtcNow;

        var result = Fill.Create(executionId, orderId, quantity, price, filledAt);

        Assert.True(result.IsSuccess);
        Assert.Equal(executionId, result.Value.ExecutionId);
        Assert.Equal(orderId, result.Value.OrderId);
        Assert.Equal(quantity, result.Value.Quantity);
        Assert.Equal(price, result.Value.Price);
        Assert.Equal(filledAt, result.Value.FilledAt);
    }
    
    [Fact]
    public void Create_WithEmptyExecutionId_ReturnsValidationFailure()
    {
        var executionId = Guid.Empty;
        var orderId = Guid.NewGuid();
        var quantity = Quantity.Create(10).Value;
        var price = Price.Create(12.34m).Value;
        var filledAt = DateTimeOffset.UtcNow;

        var result = Fill.Create(executionId, orderId, quantity, price, filledAt);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("ExecutionId cannot be empty.", result.Error.Message);
    }
}
