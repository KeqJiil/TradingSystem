using Order.Domain;
using Order.Domain.Aggregates;
using Order.Domain.Events;
using Order.Domain.VO;
using Xunit;
using OrderAggregate = Order.Domain.Aggregates.Order;

namespace Order.Tests.Domain.Aggregates;

public class OrderCreateTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static OrderTerms Terms => OrderTerms.Create(TimeInForce.Day, OrderType.Limit, Price.Create(10m).Value).Value;
    private static Quantity Qty => Quantity.Create(5).Value;

    [Fact]
    public void Create_EmptyOrderId_ReturnsValidationError()
    {
        var result = OrderAggregate.Create(Guid.Empty, Guid.NewGuid(), TestOrders.StockId, Qty, Terms, OrderSide.Buy, Now);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Invalid order id.", result.Error.Message);
    }

    [Theory]
    [InlineData((OrderSide)0)]
    [InlineData((OrderSide)99)]
    public void Create_InvalidSide_ReturnsValidationFailure(OrderSide side)
    {
        var result = OrderAggregate.Create(Guid.CreateVersion7(), Guid.NewGuid(), TestOrders.StockId, Qty, Terms, side, Now);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Invalid order side.", result.Error.Message);
    }

    [Fact]
    public void Create_EmptyStockId_ReturnsValidationFailure()
    {
        var result = OrderAggregate.Create(Guid.CreateVersion7(), Guid.NewGuid(), Guid.Empty, Qty, Terms, OrderSide.Buy, Now);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Invalid stock id.", result.Error.Message);
    }

    [Theory]
    [InlineData(OrderSide.Buy)]
    [InlineData(OrderSide.Sell)]
    public void Create_ValidArguments_ReturnsPendingNewOrder(OrderSide side)
    {
        var orderId = Guid.CreateVersion7();
        var userId = Guid.NewGuid();
        var terms = Terms;
        var quantity = Qty;

        var result = OrderAggregate.Create(orderId, userId, TestOrders.StockId, quantity, terms, side, Now);

        Assert.True(result.IsSuccess);
        var order = result.Value;
        Assert.Equal(orderId, order.Id);
        Assert.Equal(userId, order.UserId);
        Assert.Equal(quantity, order.Quantity);
        Assert.Equal(terms, order.OrderTerms);
        Assert.Equal(side, order.OrderSide);
        Assert.Equal(OrderStatus.PendingNew, order.Status);
        Assert.Equal(Now, order.CreatedAt);
        Assert.Null(order.ApprovedAt);
        Assert.Empty(order.Fills);
    }

    [Fact]
    public void Create_ValidArguments_RaisesOrderPlaced()
    {
        var userId = Guid.NewGuid();
        var terms = Terms;
        var quantity = Qty;

        var order = OrderAggregate.Create(Guid.CreateVersion7(), userId, TestOrders.StockId, quantity, terms, OrderSide.Sell, Now).Value;

        var placed = Assert.IsType<OrderPlaced>(Assert.Single(order.GetDomainEvents()));
        Assert.Equal(order.Id, placed.OrderId);
        Assert.Equal(userId, placed.UserId);
        Assert.Equal(OrderSide.Sell, placed.Side);
        Assert.Equal(quantity, placed.Quantity);
        Assert.Equal(terms, placed.OrderTerms);
        Assert.Equal(Now, placed.CreatedAt);
    }
}
