using Order.Application.UseCases.PlaceOrder;
using Order.Domain.Aggregates;
using Order.Infrastructure.Messaging.Events;
using Order.Tests.Infrastructure;
using Xunit;

namespace Order.Tests.Application.UseCases;

public class PlaceOrderCommandHandlerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task Handle_ValidLimitOrder_PersistsPendingNewOrderAndPublishesOrderPlaced()
    {
        // Arrange
        var command = new PlaceOrderCommand(
            UserId: Guid.NewGuid(),
            Symbol: Symbols.Test,
            Side: OrderSide.Buy,
            Type: OrderType.Limit,
            TimeInForce: TimeInForce.Gtc,
            Quantity: 100,
            LimitPrice: 150.00m
        );

        // Act
        var result = await _scope.SendAsync(command);

        // Assert
        Assert.True(result.IsSuccess);

        var order = await _scope.FindAsync(result.Value);
        Assert.NotNull(order);
        Assert.Equal(OrderStatus.PendingNew, order.Status);
        Assert.Equal(_scope.Clock.Now, order.CreatedAt);

        var published = Assert.Single(await _scope.Published.OfTypeAsync<OrderPlacedEvent>(result.Value.ToString()));
        Assert.Equal(command.UserId, published.Message.UserId);
    }
}
