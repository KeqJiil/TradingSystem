using Order.Application.UseCases.RejectOrder;
using Order.Domain;
using Order.Domain.Aggregates;
using TradingSystem.Contracts.ProtobufClasses.Order;
using Order.Tests.Domain.Aggregates;
using Order.Tests.Infrastructure;
using Xunit;

namespace Order.Tests.Application.UseCases;

public class RejectOrderCommandHandlerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task Handle_OrderDoesNotExist_ReturnsNotFound()
    {
        var result = await _scope.SendAsync(new RejectOrderCommand(Guid.NewGuid(), TestOrders.Reason, TestOrders.Now));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task Handle_PendingNewOrder_PersistsRejectedWithReasonAndPublishesOrderRejected()
    {
        var order = await _scope.SeedAsync(TestOrders.PendingNew());

        var result = await _scope.SendAsync(new RejectOrderCommand(order.Id, TestOrders.Reason, TestOrders.Now));

        Assert.True(result.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Rejected, updated!.Status);
        Assert.Equal(TestOrders.Reason, updated.RejectionReason);
        Assert.Equal(TestOrders.Now, updated.RejectedAt);

        var published = Assert.Single(await _scope.Published.OfTypeAsync<OrderRejectedEvent>(order.Id.ToString()));
        Assert.Equal(TestOrders.Reason, published.Message.Reason);
        Assert.Equal(TestOrders.Now, published.Message.OccurredAt.ToDateTimeOffset());
    }

    [Fact]
    public async Task Handle_UnapprovedPendingCancel_PersistsRejected()
    {
        var order = await _scope.SeedAsync(TestOrders.UnapprovedPendingCancel());

        var result = await _scope.SendAsync(new RejectOrderCommand(order.Id, TestOrders.Reason, TestOrders.Now));

        Assert.True(result.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Rejected, updated!.Status);
    }

    [Fact]
    public async Task Handle_AlreadyRejected_ReturnsSuccessAndPublishesNothing()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.Rejected));

        var result = await _scope.SendAsync(new RejectOrderCommand(order.Id, TestOrders.Reason, TestOrders.Now));

        Assert.True(result.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(TestOrders.ApprovedAt, updated!.RejectedAt);
        Assert.Empty(await _scope.Published.OfTypeAsync<OrderRejectedEvent>(order.Id.ToString()));
    }

    [Fact]
    public async Task Handle_ApprovedOrder_ReturnsViolationAndKeepsNew()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.New));

        var result = await _scope.SendAsync(new RejectOrderCommand(order.Id, TestOrders.Reason, TestOrders.Now));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Violation, result.Error.Type);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.New, updated!.Status);
    }
}
