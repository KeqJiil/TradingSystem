using Google.Protobuf.WellKnownTypes;
using Order.Domain;
using Order.Domain.Aggregates;
using Order.Presentation.Grpc.Mapping;
using Order.Tests.Domain.Aggregates;
using Xunit;
using Proto = TradingSystem.Contracts.ProtobufClasses.Order;

namespace Order.Tests.Presentation.Grpc.Mapping;

public class RequestMappingTests
{
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void ToCommand_ValidLimitOrder_MapsAllFields()
    {
        var result = LimitOrder().ToCommand(_userId);

        Assert.True(result.IsSuccess);
        var command = result.Value;
        Assert.Equal(_userId, command.UserId);
        Assert.Equal(Symbols.Test, command.Symbol);
        Assert.Equal(10, command.Quantity);
        Assert.Equal(OrderSide.Sell, command.Side);
        Assert.Equal(TimeInForce.Gtc, command.TimeInForce);
        Assert.Equal(OrderType.Limit, command.Type);
        Assert.Equal(12.5m, command.LimitPrice);
    }

    [Fact]
    public void ToCommand_MarketOrderWithoutLimitPrice_MapsNullLimitPrice()
    {
        var request = LimitOrder();
        request.OrderType = Proto.OrderType.Market;
        request.ClearLimitPrice();

        var result = request.ToCommand(_userId);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.LimitPrice);
    }

    [Theory]
    [InlineData("symbol")]
    [InlineData("order side")]
    [InlineData("time in force")]
    [InlineData("order type")]
    public void ToCommand_UnspecifiedEnum_ReturnsValidationNamingField(string field)
    {
        var request = LimitOrder();
        switch (field)
        {
            case "symbol": request.Symbol = Proto.Symbol.Unspecified; break;
            case "order side": request.OrderSide = Proto.OrderSide.Unspecified; break;
            case "time in force": request.TimeInForce = Proto.TimeInForce.Unspecified; break;
            case "order type": request.OrderType = Proto.OrderType.Unspecified; break;
        }

        AssertValidation(request.ToCommand(_userId), $"Invalid {field}.");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12,5")]
    [InlineData("1,000.5")]
    [InlineData(" 12.5")]
    [InlineData("-3")]
    [InlineData("1e3")]
    [InlineData("")]
    public void ToCommand_MalformedLimitPrice_ReturnsValidation(string limitPrice)
    {
        var request = LimitOrder();
        request.LimitPrice = limitPrice;

        AssertValidation(request.ToCommand(_userId), "Invalid limit price.");
    }

    [Fact]
    public void ToCommand_RequestCancelWithValidId_MapsOrderAndUser()
    {
        var orderId = Guid.NewGuid();

        var result = new Proto.RequestOrderCancelRequest { OrderId = orderId.ToString() }.ToCommand(_userId);

        Assert.True(result.IsSuccess);
        Assert.Equal(orderId, result.Value.OrderId);
        Assert.Equal(_userId, result.Value.UserId);
    }

    [Fact]
    public void ToCommand_RequestCancelWithMalformedId_ReturnsValidation()
    {
        var result = new Proto.RequestOrderCancelRequest { OrderId = "nope" }.ToCommand(_userId);

        AssertValidation(result, "Invalid order id.");
    }

    [Fact]
    public void ToQuery_GetOrderWithValidId_MapsOrderAndUser()
    {
        var orderId = Guid.NewGuid();

        var result = new Proto.GetOrderRequest { OrderId = orderId.ToString() }.ToQuery(_userId);

        Assert.True(result.IsSuccess);
        Assert.Equal(orderId, result.Value.OrderId);
        Assert.Equal(_userId, result.Value.UserId);
    }

    [Fact]
    public void ToQuery_GetOrderWithMalformedId_ReturnsValidation()
    {
        var result = new Proto.GetOrderRequest { OrderId = "" }.ToQuery(_userId);

        AssertValidation(result, "Invalid order id.");
    }

    [Fact]
    public void ToQuery_ListWithoutOptionalFields_MapsNulls()
    {
        var result = new Proto.GetOrdersByUserIdRequest { Limit = 20 }.ToQuery(_userId);

        Assert.True(result.IsSuccess);
        var query = result.Value;
        Assert.Equal(_userId, query.UserId);
        Assert.Equal(20, query.Limit);
        Assert.Null(query.Status);
        Assert.Null(query.AfterCreatedAt);
        Assert.Null(query.AfterId);
    }

    [Fact]
    public void ToQuery_ListWithStatusAndCursor_MapsAll()
    {
        var afterId = Guid.NewGuid();
        var request = new Proto.GetOrdersByUserIdRequest
        {
            Limit = 5,
            Status = Proto.OrderStatus.PartiallyFilled,
            AfterCreatedAt = Timestamp.FromDateTimeOffset(TestOrders.Now),
            AfterId = afterId.ToString()
        };

        var result = request.ToQuery(_userId);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.PartiallyFilled, result.Value.Status);
        Assert.Equal(TestOrders.Now, result.Value.AfterCreatedAt);
        Assert.Equal(afterId, result.Value.AfterId);
    }

    [Fact]
    public void ToQuery_ListWithExplicitUnspecifiedStatus_ReturnsValidation()
    {
        var request = new Proto.GetOrdersByUserIdRequest { Limit = 5, Status = Proto.OrderStatus.Unspecified };

        AssertValidation(request.ToQuery(_userId), "Invalid status.");
    }

    [Fact]
    public void ToQuery_ListWithMalformedAfterId_ReturnsValidation()
    {
        var request = new Proto.GetOrdersByUserIdRequest { Limit = 5, AfterId = "nope" };

        AssertValidation(request.ToQuery(_userId), "Invalid after id.");
    }

    private static Proto.CreateOrderRequest LimitOrder() => new()
    {
        Symbol = Proto.Symbol.Test,
        Quantity = 10,
        OrderSide = Proto.OrderSide.Sell,
        TimeInForce = Proto.TimeInForce.Gtc,
        OrderType = Proto.OrderType.Limit,
        LimitPrice = "12.5"
    };

    private static void AssertValidation<T>(Result<T> result, string message)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(message, result.Error.Message);
    }
}
