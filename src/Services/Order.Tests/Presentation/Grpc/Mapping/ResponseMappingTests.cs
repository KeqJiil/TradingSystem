using Order.Application.Abstractions;
using Order.Domain.Aggregates;
using Order.Presentation.Grpc.Mapping;
using Order.Tests.Domain.Aggregates;
using Xunit;
using Proto = TradingSystem.Contracts.ProtobufClasses.Order;

namespace Order.Tests.Presentation.Grpc.Mapping;

public class ResponseMappingTests
{
    [Fact]
    public void ToResponse_DetailsWithFills_MapsAllFields()
    {
        var order = Order(OrderStatus.Canceled, limitPrice: 12.5m);
        var fill = new FillDto(Guid.NewGuid(), 3, 12.25m, TestOrders.Now);
        var details = new OrderDetailsDto(order, TimeInForce.Gtc, TestOrders.ApprovedAt, TestOrders.Now, "user request",
            [fill]);

        var response = details.ToResponse();

        Assert.Equal(order.Id.ToString(), response.Order.Id);
        Assert.Equal(Proto.Symbol.Test, response.Order.Symbol);
        Assert.Equal(Proto.OrderSide.Sell, response.Order.Side);
        Assert.Equal(Proto.OrderType.Limit, response.Order.Type);
        Assert.Equal("12.5", response.Order.LimitPrice);
        Assert.Equal("10", response.Order.Quantity);
        Assert.Equal("3", response.Order.FilledQuantity);
        Assert.Equal(Proto.OrderStatus.Canceled, response.Order.Status);
        Assert.Equal(TestOrders.CreatedAt, response.Order.CreatedAt.ToDateTimeOffset());
        Assert.Equal(Proto.TimeInForce.Gtc, response.TimeInForce);
        Assert.Equal(TestOrders.ApprovedAt, response.ApprovedAt.ToDateTimeOffset());
        Assert.Equal(TestOrders.Now, response.ClosedAt.ToDateTimeOffset());
        Assert.Equal("user request", response.CancelReason);
        var mappedFill = Assert.Single(response.Fills);
        Assert.Equal(fill.ExecutionId.ToString(), mappedFill.ExecutionId);
        Assert.Equal("3", mappedFill.Quantity);
        Assert.Equal("12.25", mappedFill.Price);
        Assert.Equal(TestOrders.Now, mappedFill.FilledAt.ToDateTimeOffset());
    }

    [Fact]
    public void ToResponse_DetailsWithoutOptionalValues_LeavesFieldsUnset()
    {
        var details = new OrderDetailsDto(Order(OrderStatus.PendingNew, limitPrice: null), TimeInForce.Ioc, null,
            null, null, []);

        var response = details.ToResponse();

        Assert.False(response.Order.HasLimitPrice);
        Assert.Null(response.ApprovedAt);
        Assert.Null(response.ClosedAt);
        Assert.False(response.HasCancelReason);
        Assert.Empty(response.Fills);
    }

    [Fact]
    public void ToResponse_OrderList_KeepsOrder()
    {
        var first = Order(OrderStatus.New, limitPrice: null);
        var second = Order(OrderStatus.Filled, limitPrice: 1m);

        var response = new List<OrderDto> { first, second }.ToResponse();

        Assert.Equal([first.Id.ToString(), second.Id.ToString()], response.Orders.Select(o => o.Id));
    }

    private static OrderDto Order(OrderStatus status, decimal? limitPrice) =>
        new(Guid.NewGuid(), Symbols.Test, OrderSide.Sell, limitPrice is null ? OrderType.Market : OrderType.Limit,
            limitPrice, 10, 3, status, TestOrders.CreatedAt);
}
