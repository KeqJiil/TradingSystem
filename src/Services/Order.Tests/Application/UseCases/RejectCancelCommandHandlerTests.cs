using Order.Application.UseCases.RejectCancel;
using Order.Domain;
using Order.Domain.Aggregates;
using Order.Tests.Domain.Aggregates;
using Order.Tests.Infrastructure;
using Xunit;

namespace Order.Tests.Application.UseCases;

public class RejectCancelCommandHandlerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task Handle_OrderDoesNotExist_ReturnsNotFound()
    {
        var result = await _scope.SendAsync(new RejectCancelCommand(Guid.NewGuid(), TestOrders.Reason, TestOrders.Now));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task Handle_ApprovedPendingCancelWithoutFills_PersistsNew()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.PendingCancel));

        var result = await _scope.SendAsync(new RejectCancelCommand(order.Id, TestOrders.Reason, TestOrders.Now));

        Assert.True(result.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.New, updated!.Status);
    }

    [Fact]
    public async Task Handle_PartiallyFilledPendingCancel_PersistsPartiallyFilled()
    {
        var order = await _scope.SeedAsync(TestOrders.PartiallyFilledPendingCancel());

        var result = await _scope.SendAsync(new RejectCancelCommand(order.Id, TestOrders.Reason, TestOrders.Now));

        Assert.True(result.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.PartiallyFilled, updated!.Status);
    }

    [Fact]
    public async Task Handle_UnapprovedPendingCancel_PersistsPendingNew()
    {
        var order = await _scope.SeedAsync(TestOrders.UnapprovedPendingCancel());

        var result = await _scope.SendAsync(new RejectCancelCommand(order.Id, TestOrders.Reason, TestOrders.Now));

        Assert.True(result.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.PendingNew, updated!.Status);
    }

    [Fact]
    public async Task Handle_FilledOrder_ReturnsSuccessAndKeepsFilled()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.Filled));

        var result = await _scope.SendAsync(new RejectCancelCommand(order.Id, TestOrders.Reason, TestOrders.Now));

        Assert.True(result.IsSuccess);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Filled, updated!.Status);
    }

    [Fact]
    public async Task Handle_NewOrder_ReturnsViolation()
    {
        var order = await _scope.SeedAsync(TestOrders.InStatus(OrderStatus.New));

        var result = await _scope.SendAsync(new RejectCancelCommand(order.Id, TestOrders.Reason, TestOrders.Now));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Violation, result.Error.Type);
        var updated = await _scope.FindAsync(order.Id);
        Assert.Equal(OrderStatus.New, updated!.Status);
    }
}
