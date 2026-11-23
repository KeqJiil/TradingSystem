using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Order.Application.UseCases.PlaceOrder;
using Order.Domain;
using Order.Domain.Aggregates;
using TradingSystem.Contracts.ProtobufClasses.Order;
using Order.Tests.Domain.Aggregates;
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
        var stock = await _scope.SeedStockAsync(TestStocks.AlwaysOpen());
        var command = new PlaceOrderCommand(
            UserId: Guid.NewGuid(),
            StockId: stock.Id,
            Side: OrderSide.Buy,
            Type: OrderType.Limit,
            TimeInForce: TimeInForce.Gtc,
            Quantity: 100,
            LimitPrice: 150.00m,
            OrderId: Guid.CreateVersion7()
        );

        var result = await _scope.SendAsync(command);

        Assert.True(result.IsSuccess);

        var order = await _scope.FindAsync(result.Value);
        Assert.NotNull(order);
        Assert.Equal(OrderStatus.PendingNew, order.Status);
        Assert.Equal(_scope.Clock.Now, order.CreatedAt);

        var published = Assert.Single(await _scope.Published.OfTypeAsync<OrderPlacedEvent>(result.Value.ToString()));
        Assert.Equal(command.UserId.ToString(), published.Message.UserId);
        Assert.Equal(stock.Id, order.StockId);
    }

    [Fact]
    public async Task Handle_UnknownStock_ReturnsNotFoundAndPersistsNothing()
    {
        var command = NewCommand(Guid.NewGuid());

        var result = await _scope.SendAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Null(await _scope.FindAsync(command.OrderId));
    }

    [Fact]
    public async Task Handle_StockNotOpenToTrade_ReturnsViolationAndPersistsNothing()
    {
        var stock = await _scope.SeedStockAsync(TestStocks.Closed());
        var command = NewCommand(stock.Id);

        var result = await _scope.SendAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Violation, result.Error.Type);
        Assert.Null(await _scope.FindAsync(command.OrderId));
    }
    
    [Fact]
    public async Task Handle_InvalidQuantity_PersistsNothingAndPublishesNothing()
    {
        var stock = await _scope.SeedStockAsync(TestStocks.AlwaysOpen());
        var command = new PlaceOrderCommand(
            UserId: Guid.NewGuid(),
            StockId: stock.Id,
            Side: OrderSide.Buy,
            Type: OrderType.Limit,
            TimeInForce: TimeInForce.Gtc,
            Quantity: -1,
            LimitPrice: 150.00m,
            OrderId: Guid.CreateVersion7()
        );
        
        var result = await _scope.SendAsync(command);

        Assert.True(result.IsFailure);
        var published = await _scope.Published.OfTypeAsync<OrderPlacedEvent>();
        Assert.DoesNotContain(published, m => m.Message.UserId == command.UserId.ToString());
    }

    [Fact]
    public async Task Handle_SameOrderIdTwice_ThrowsDbUpdateException()
    {
        var stock = await _scope.SeedStockAsync(TestStocks.AlwaysOpen());
        var command = new PlaceOrderCommand(
            UserId: Guid.NewGuid(),
            StockId: stock.Id,
            Side: OrderSide.Buy,
            Type: OrderType.Limit,
            TimeInForce: TimeInForce.Gtc,
            Quantity: 100,
            LimitPrice: 150.00m,
            OrderId: Guid.CreateVersion7()
        );

        var first = await _scope.SendAsync(command);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => _scope.SendAsync(command));

        Assert.True(first.IsSuccess);
        Assert.Equal(2627, Assert.IsType<SqlException>(exception.InnerException).Number);
        Assert.Single(await _scope.Published.OfTypeAsync<OrderPlacedEvent>(command.OrderId.ToString()));
    }

    private static PlaceOrderCommand NewCommand(Guid stockId) => new(
        UserId: Guid.NewGuid(),
        StockId: stockId,
        Side: OrderSide.Buy,
        Type: OrderType.Limit,
        TimeInForce: TimeInForce.Gtc,
        Quantity: 100,
        LimitPrice: 150.00m,
        OrderId: Guid.CreateVersion7());
}
