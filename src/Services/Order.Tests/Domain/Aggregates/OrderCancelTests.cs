using Order.Domain;
using Order.Domain.Aggregates;
using Order.Domain.Events;
using Xunit;
using OrderAggregate = Order.Domain.Aggregates.Order;
using static Order.Tests.Domain.Aggregates.TestOrders;

namespace Order.Tests.Domain.Aggregates;

public class OrderCancelTests
{
    [Fact]
    public void Cancel_AlreadyCanceled_DoesNothing()
    {
        var order = InStatus(OrderStatus.Canceled);

        var result = order.Cancel("other", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Canceled, order.Status);
        Assert.Equal(Reason, order.CancellationReason);
        Assert.Equal(ApprovedAt, order.CanceledAt);
        Assert.Empty(order.GetDomainEvents());
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.PartiallyFilled)]
    [InlineData(OrderStatus.PendingCancel)]
    public void Cancel_CancelableStatus_CancelsAndRaisesOrderCanceled(OrderStatus status)
    {
        var order = InStatus(status);

        var result = order.Cancel("user request", Now);

        Assert.True(result.IsSuccess);
        AssertCanceled(order);
    }

    [Fact]
    public void Cancel_UnapprovedPendingCancel_CancelsAndRaisesOrderCanceled()
    {
        var order = UnapprovedPendingCancel();

        var result = order.Cancel("user request", Now);

        Assert.True(result.IsSuccess);
        AssertCanceled(order);
    }

    [Theory]
    [InlineData(OrderStatus.PendingNew)]
    [InlineData(OrderStatus.Rejected)]
    [InlineData(OrderStatus.Filled)]
    [InlineData(OrderStatus.Expired)]
    public void Cancel_NotCancelableStatus_ReturnsViolation(OrderStatus status)
    {
        var order = InStatus(status);

        var result = order.Cancel("user request", Now);

        AssertFailure(result, ErrorType.Violation, "Order can only be canceled from New or PartiallyFilled status.");
        Assert.Equal(status, order.Status);
        Assert.Null(order.CancellationReason);
        Assert.Null(order.CanceledAt);
        Assert.Empty(order.GetDomainEvents());
    }

    private static void AssertCanceled(OrderAggregate order)
    {
        Assert.Equal(OrderStatus.Canceled, order.Status);
        Assert.Equal("user request", order.CancellationReason);
        Assert.Equal(Now, order.CanceledAt);
        var canceled = Assert.IsType<OrderCanceled>(Assert.Single(order.GetDomainEvents()));
        Assert.Equal(order.Id, canceled.OrderId);
        Assert.Equal("user request", canceled.Reason);
        Assert.Equal(Now, canceled.CancelledAt);
    }
}
