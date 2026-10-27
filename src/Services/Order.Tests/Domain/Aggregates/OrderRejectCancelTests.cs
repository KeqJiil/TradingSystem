using Order.Domain;
using Order.Domain.Aggregates;
using Order.Domain.Events;
using Xunit;
using OrderAggregate = Order.Domain.Aggregates.Order;
using static Order.Tests.Domain.Aggregates.TestOrders;

namespace Order.Tests.Domain.Aggregates;

public class OrderRejectCancelTests
{
    [Theory]
    [InlineData(OrderStatus.Rejected)]
    [InlineData(OrderStatus.Filled)]
    [InlineData(OrderStatus.Canceled)]
    [InlineData(OrderStatus.Expired)]
    public void RejectCancel_TerminalStatus_DoesNothing(OrderStatus status)
    {
        var order = InStatus(status);

        var result = order.RejectCancel("too late", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(status, order.Status);
        Assert.Empty(order.GetDomainEvents());
    }

    [Theory]
    [InlineData(OrderStatus.PendingNew)]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.PartiallyFilled)]
    public void RejectCancel_NotPendingCancel_ReturnsViolation(OrderStatus status)
    {
        var order = InStatus(status);

        var result = order.RejectCancel("too late", Now);

        AssertFailure(result, ErrorType.Violation, "Order can only reject cancel from PendingCancel status.");
        Assert.Equal(status, order.Status);
        Assert.Empty(order.GetDomainEvents());
    }

    [Fact]
    public void RejectCancel_UnapprovedPendingCancel_RestoresPendingNew()
    {
        var order = UnapprovedPendingCancel();

        var result = order.RejectCancel("too late", Now);

        Assert.True(result.IsSuccess);
        AssertCancelRejected(order, OrderStatus.PendingNew);
    }

    [Fact]
    public void RejectCancel_ApprovedPendingCancelWithoutFills_RestoresNew()
    {
        var order = InStatus(OrderStatus.PendingCancel);

        var result = order.RejectCancel("too late", Now);

        Assert.True(result.IsSuccess);
        AssertCancelRejected(order, OrderStatus.New);
    }

    [Fact]
    public void RejectCancel_ApprovedPendingCancelWithFills_RestoresPartiallyFilled()
    {
        var order = PartiallyFilledPendingCancel();

        var result = order.RejectCancel("too late", Now);

        Assert.True(result.IsSuccess);
        AssertCancelRejected(order, OrderStatus.PartiallyFilled);
    }

    private static void AssertCancelRejected(OrderAggregate order, OrderStatus expectedStatus)
    {
        Assert.Equal(expectedStatus, order.Status);
        var rejected = Assert.IsType<OrderCancelRejected>(Assert.Single(order.GetDomainEvents()));
        Assert.Equal(order.Id, rejected.OrderId);
        Assert.Equal("too late", rejected.Reason);
        Assert.Equal(Now, rejected.RejectedAt);
    }
}
