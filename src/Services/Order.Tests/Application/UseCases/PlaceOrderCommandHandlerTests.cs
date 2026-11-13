using Order.Application.UseCases.PlaceOrder;
using Order.Domain.Aggregates;
using TradingSystem.Contracts.ProtobufClasses.Order;
using Order.Tests.Infrastructure;
using Xunit;
using OrderSide = Order.Domain.Aggregates.OrderSide;
using OrderStatus = Order.Domain.Aggregates.OrderStatus;
using OrderType = Order.Domain.Aggregates.OrderType;
using TimeInForce = Order.Domain.Aggregates.TimeInForce;

namespace Order.Tests.Application.UseCases;

public class PlaceOrderCommandHandlerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task Handle_ValidLimitOrder_PersistsPendingNewOrderAndPublishesOrderPlaced()
    {
        var command = new PlaceOrderCommand(
            UserId: Guid.NewGuid(),
            Symbol: Symbols.Test,
            Side: OrderSide.Buy,
            Type: OrderType.Limit,
            TimeInForce: TimeInForce.Gtc,
            Quantity: 100,
            LimitPrice: 150.00m
        );

        var result = await _scope.SendAsync(command);

        Assert.True(result.IsSuccess);

        var order = await _scope.FindAsync(result.Value);
        Assert.NotNull(order);
        Assert.Equal(OrderStatus.PendingNew, order.Status);
        Assert.Equal(_scope.Clock.Now, order.CreatedAt);

        var published = Assert.Single(await _scope.Published.OfTypeAsync<OrderPlacedEvent>(result.Value.ToString()));
        Assert.Equal(command.UserId.ToString(), published.Message.UserId);
    }
    
    [Fact]
    public async Task Handle_InvalidQuantity_PersistsNothingAndPublishesNothing()
    {
        var command = new PlaceOrderCommand(
            UserId: Guid.NewGuid(),
            Symbol: Symbols.Test,
            Side: OrderSide.Buy,
            Type: OrderType.Limit,
            TimeInForce: TimeInForce.Gtc,
            Quantity: -1,
            LimitPrice: 150.00m
        );
        
        var result = await _scope.SendAsync(command);

        Assert.True(result.IsFailure);
        var published = await _scope.Published.OfTypeAsync<OrderPlacedEvent>();
        Assert.DoesNotContain(published, m => m.Message.UserId == command.UserId.ToString());
    }
}
