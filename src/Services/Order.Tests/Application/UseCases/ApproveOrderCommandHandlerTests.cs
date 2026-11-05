using Order.Application.UseCases.ApproveOrder;
using Order.Domain;
using Order.Domain.Aggregates;
using Order.Tests.Domain.Aggregates;
using Order.Tests.Infrastructure;
using Xunit;

namespace Order.Tests.Application.UseCases;

public class ApproveOrderCommandHandlerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task Handle_OrderDoesNotExist_ReturnsNotFound()
    {
        var result = await _scope.SendAsync(new ApproveOrderCommand(Guid.NewGuid(), _scope.Clock.Now));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task Handle_PendingNewOrder_PersistsNewWithApprovedAt()
    {
        var order = await _scope.SeedAsync(TestOrders.PendingNew());

        var result = await _scope.SendAsync(new ApproveOrderCommand(order.Id, _scope.Clock.Now));

        Assert.True(result.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.New, updated!.Status);
        Assert.Equal(_scope.Clock.Now, updated.ApprovedAt);
    }

    [Fact]
    public async Task Handle_UnapprovedPendingCancel_PersistsApprovedAtAndKeepsPendingCancel()
    {
        var order = await _scope.SeedAsync(TestOrders.UnapprovedPendingCancel());

        var result = await _scope.SendAsync(new ApproveOrderCommand(order.Id, _scope.Clock.Now));

        Assert.True(result.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.PendingCancel, updated!.Status);
        Assert.Equal(_scope.Clock.Now, updated.ApprovedAt);
    }

    [Fact]
    public async Task Handle_AlreadyApproved_ReturnsSuccessAndKeepsOriginalApprovedAt()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.New));

        var result = await _scope.SendAsync(new ApproveOrderCommand(order.Id, _scope.Clock.Now));

        Assert.True(result.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.New, updated!.Status);
        Assert.Equal(TestOrders.ApprovedAt, updated.ApprovedAt);
    }

    [Fact]
    public async Task Handle_RejectedOrder_ReturnsViolationAndKeepsRejected()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.Rejected));

        var result = await _scope.SendAsync(new ApproveOrderCommand(order.Id, _scope.Clock.Now));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Violation, result.Error.Type);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Rejected, updated!.Status);
        Assert.Null(updated.ApprovedAt);
    }
}
