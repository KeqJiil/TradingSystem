using Order.Application.UseCases.CancelOrder;
using Order.Domain;
using Order.Domain.Aggregates;
using TradingSystem.Contracts.ProtobufClasses.Order;
using Order.Tests.Domain.Aggregates;
using Order.Tests.Infrastructure;
using Xunit;
using OrderStatus = Order.Domain.Aggregates.OrderStatus;

namespace Order.Tests.Application.UseCases;

public class CancelOrderCommandHandlerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task Handle_OrderDoesNotExist_ReturnsNotFound()
    {
        var result = await _scope.SendAsync(new CancelOrderCommand(Guid.NewGuid(), TestOrders.Reason, TestOrders.Now));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task Handle_PendingCancelOrder_PersistsCanceledWithReasonAndPublishesOrderCanceled()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.PendingCancel));

        var result = await _scope.SendAsync(new CancelOrderCommand(order.Id, TestOrders.Reason, TestOrders.Now));

        Assert.True(result.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Canceled, updated!.Status);
        Assert.Equal(TestOrders.Reason, updated.CancellationReason);
        Assert.Equal(TestOrders.Now, updated.CanceledAt);

        var published = Assert.Single(await _scope.Published.OfTypeAsync<OrderCanceledEvent>(order.Id.ToString()));
        Assert.Equal(TestOrders.Reason, published.Message.Reason);
        Assert.Equal(TestOrders.Now, published.Message.OccurredAt.ToDateTimeOffset());
    }

    [Fact]
    public async Task Handle_PartiallyFilledOrder_PersistsCanceledAndPublishesFilledAndRemainingQuantity()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.PartiallyFilled));

        var result = await _scope.SendAsync(new CancelOrderCommand(order.Id, TestOrders.Reason, TestOrders.Now));

        Assert.True(result.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Canceled, updated!.Status);
        Assert.Equal(order.FilledQuantity, updated.FilledQuantity);
        Assert.Single(updated.Fills);

        var published = Assert.Single(await _scope.Published.OfTypeAsync<OrderCanceledEvent>(order.Id.ToString()));
        Assert.Equal(1, published.Message.FilledQuantity);
        Assert.Equal(TestOrders.Quantity - 1, published.Message.CanceledQuantity);
    }

    [Fact]
    public async Task Handle_AlreadyCanceled_ReturnsSuccessAndPublishesNothing()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.Canceled));

        var result = await _scope.SendAsync(new CancelOrderCommand(order.Id, TestOrders.Reason, TestOrders.Now));

        Assert.True(result.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(TestOrders.ApprovedAt, updated!.CanceledAt);
        Assert.Empty(await _scope.Published.OfTypeAsync<OrderCanceledEvent>(order.Id.ToString()));
    }

    [Fact]
    public async Task Handle_FilledOrder_ReturnsViolationAndKeepsFilled()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.Filled));

        var result = await _scope.SendAsync(new CancelOrderCommand(order.Id, TestOrders.Reason, TestOrders.Now));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Violation, result.Error.Type);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Filled, updated!.Status);
    }
}
