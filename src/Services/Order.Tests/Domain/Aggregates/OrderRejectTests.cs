using Order.Domain;
using Order.Domain.Aggregates;
using Order.Domain.Events;
using Xunit;
using OrderAggregate = Order.Domain.Aggregates.Order;
using static Order.Tests.Domain.Aggregates.TestOrders;

namespace Order.Tests.Domain.Aggregates;

public class OrderRejectTests
{
    [Fact]
    public void Reject_AlreadyRejected_DoesNothing()
    {
        var order = InStatus(OrderStatus.Rejected);

        var result = order.Reject("other", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Equal(Reason, order.RejectionReason);
        Assert.Equal(ApprovedAt, order.RejectedAt);
        Assert.Empty(order.GetDomainEvents());
    }

    [Fact]
    public void Reject_PendingNew_RejectsAndRaisesOrderRejected()
    {
        var order = InStatus(OrderStatus.PendingNew);

        var result = order.Reject("no funds", Now);

        Assert.True(result.IsSuccess);
        AssertRejected(order, "no funds");
    }

    [Fact]
    public void Reject_UnapprovedPendingCancel_RejectsAndRaisesOrderRejected()
    {
        var order = UnapprovedPendingCancel();

        var result = order.Reject("no funds", Now);

        Assert.True(result.IsSuccess);
        AssertRejected(order, "no funds");
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.PartiallyFilled)]
    [InlineData(OrderStatus.PendingCancel)]
    [InlineData(OrderStatus.Filled)]
    [InlineData(OrderStatus.Canceled)]
    [InlineData(OrderStatus.Expired)]
    public void Reject_ApprovedOrder_ReturnsViolation(OrderStatus status)
    {
        var order = InStatus(status);

        var result = order.Reject("no funds", Now);

        AssertFailure(result, ErrorType.Violation,
            "Order can only be rejected from PendingNew or unapproved PendingCancel status.");
        Assert.Equal(status, order.Status);
        Assert.Null(order.RejectionReason);
        Assert.Null(order.RejectedAt);
        Assert.Empty(order.GetDomainEvents());
    }

    private static void AssertRejected(OrderAggregate order, string reason)
    {
        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Equal(reason, order.RejectionReason);
        Assert.Equal(Now, order.RejectedAt);
        var rejected = Assert.IsType<OrderRejected>(Assert.Single(order.GetDomainEvents()));
        Assert.Equal(order.Id, rejected.OrderId);
        Assert.Equal(reason, rejected.Reason);
        Assert.Equal(Now, rejected.RejectedAt);
    }
}
