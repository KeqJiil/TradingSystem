using Order.Application.UseCases.RequestCancel;
using Order.Domain;
using Order.Domain.Aggregates;
using Order.Domain.VO;
using Order.Tests.Infrastructure;
using OrderAggregate = Order.Domain.Aggregates.Order;
using Xunit;

namespace Order.Tests.Application.UseCases;

public class RequestCancelCommandHandlerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();
    
    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenOrderDoesNotExist()
    {
        var command = new RequestCancelCommand(Guid.NewGuid(), Guid.NewGuid());

        var result = await _scope.SendAsync(command);
        
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenUserIsNotOwnerOfOrder()
    {
        var orderAggregate = CreateOrder().Value;
        
        var order = await _scope.SeedAsync(orderAggregate);
        var command = new RequestCancelCommand(order.Id, Guid.NewGuid());

        var result = await _scope.SendAsync(command);
        
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }
    
    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenOrderCannotBeCancelled()
    {
        var userId = Guid.NewGuid();
        var orderAggregate = CreateOrder(userId, timeInForce: TimeInForce.Ioc).Value;
        orderAggregate.Reject("", DateTime.Now);
        
        var order = await _scope.SeedAsync(orderAggregate);
        var command = new RequestCancelCommand(order.Id, userId);

        var result = await _scope.SendAsync(command);
        
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }
    
    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenOrderIsRequestedToCancel()
    {
        var userId = Guid.NewGuid();
        var orderAggregate = CreateOrder(userId).Value;
        orderAggregate.Approve(DateTime.Now);
        
        var order = await _scope.SeedAsync(orderAggregate);
        var command = new RequestCancelCommand(order.Id, userId);

        var result = await _scope.SendAsync(command);
        
        var updatedOrder = await _scope.FindAsync(order.Id);
        
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.PendingCancel, updatedOrder!.Status);
    }
    
    private Result<OrderAggregate> CreateOrder(Guid? userId = null, decimal? priceValue = null, int? quantity = null, TimeInForce? timeInForce = null, OrderType? orderType = null, OrderSide? orderSide = null)
    {
        return OrderAggregate.Create(userId ?? Guid.NewGuid(), 
            Symbols.Test, Quantity.Create(quantity ?? 1).Value, 
            OrderTerms.Create(timeInForce ?? TimeInForce.Gtc, orderType ?? OrderType.Limit, Price.Create(priceValue ?? 100).Value).Value,
            orderSide ?? OrderSide.Buy,
            DateTime.Now);
    }
}
