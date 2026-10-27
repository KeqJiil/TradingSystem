using Order.Domain;
using Order.Domain.Aggregates;
using Order.Domain.Events;
using Xunit;
using static Order.Tests.Domain.Aggregates.TestOrders;

namespace Order.Tests.Domain.Aggregates;

public class OrderRequestCancelTests
{
    [Theory]
    [InlineData(OrderStatus.Canceled)]
    [InlineData(OrderStatus.PendingCancel)]
    public void RequestCancel_AlreadyCanceledOrPendingCancel_DoesNothing(OrderStatus status)
    {
        var order = InStatus(status);

        var result = order.RequestCancel(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(status, order.Status);
        Assert.Empty(order.GetDomainEvents());
    }

    [Theory]
    [InlineData(OrderStatus.PendingNew)]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.PartiallyFilled)]
    public void RequestCancel_ActiveOrder_SetsPendingCancelAndRaisesOrderCancelRequested(OrderStatus status)
    {
        var order = InStatus(status);

        var result = order.RequestCancel(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.PendingCancel, order.Status);
        var requested = Assert.IsType<OrderCancelRequested>(Assert.Single(order.GetDomainEvents()));
        Assert.Equal(order.Id, requested.OrderId);
        Assert.Equal(Now, requested.RequestedAt);
    }

    [Theory]
    [InlineData(OrderStatus.Rejected)]
    [InlineData(OrderStatus.Filled)]
    [InlineData(OrderStatus.Expired)]
    public void RequestCancel_TerminalStatus_ReturnsConflict(OrderStatus status)
    {
        var order = InStatus(status);

        var result = order.RequestCancel(Now);

        AssertFailure(result, ErrorType.Conflict, "Order can only request cancel from New or PartiallyFilled status.");
        Assert.Equal(status, order.Status);
        Assert.Empty(order.GetDomainEvents());
    }
}
