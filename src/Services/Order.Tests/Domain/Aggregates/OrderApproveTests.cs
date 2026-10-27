using Order.Domain;
using Order.Domain.Aggregates;
using Order.Domain.Events;
using Xunit;

namespace Order.Tests.Domain.Aggregates;

public class OrderApproveTests
{
    private static readonly DateTimeOffset Now = TestOrders.ApprovedAt.AddMinutes(5);

    [Fact]
    public void Approve_RejectedOrder_ReturnsViolation()
    {
        var order = TestOrders.InStatus(OrderStatus.Rejected);

        var result = order.Approve(Now);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Violation, result.Error.Type);
        Assert.Equal("Rejected order cannot be accepted.", result.Error.Message);
        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Null(order.ApprovedAt);
        Assert.Empty(order.GetDomainEvents());
    }

    [Fact]
    public void Approve_PendingNew_SetsNewStatusApprovedAtAndRaisesOrderApproved()
    {
        var order = TestOrders.InStatus(OrderStatus.PendingNew);

        var result = order.Approve(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.New, order.Status);
        Assert.Equal(Now, order.ApprovedAt);
        var approved = Assert.IsType<OrderApproved>(Assert.Single(order.GetDomainEvents()));
        Assert.Equal(order.Id, approved.OrderId);
        Assert.Equal(Now, approved.ApprovedAt);
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.PartiallyFilled)]
    [InlineData(OrderStatus.Filled)]
    [InlineData(OrderStatus.Canceled)]
    [InlineData(OrderStatus.Expired)]
    public void Approve_AlreadyApprovedOrder_DoesNothing(OrderStatus status)
    {
        var order = TestOrders.InStatus(status);

        var result = order.Approve(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(status, order.Status);
        Assert.Equal(TestOrders.ApprovedAt, order.ApprovedAt);
        Assert.Empty(order.GetDomainEvents());
    }

    [Fact]
    public void Approve_UnapprovedPendingCancel_SetsApprovedAtAndRaisesOrderApproved()
    {
        var order = TestOrders.UnapprovedPendingCancel();

        var result = order.Approve(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.PendingCancel, order.Status);
        Assert.Equal(Now, order.ApprovedAt);
        var approved = Assert.IsType<OrderApproved>(Assert.Single(order.GetDomainEvents()));
        Assert.Equal(order.Id, approved.OrderId);
        Assert.Equal(Now, approved.ApprovedAt);
    }

    [Fact]
    public void Approve_ApprovedPendingCancel_DoesNothing()
    {
        var order = TestOrders.InStatus(OrderStatus.PendingCancel);

        var result = order.Approve(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.PendingCancel, order.Status);
        Assert.Equal(TestOrders.ApprovedAt, order.ApprovedAt);
        Assert.Empty(order.GetDomainEvents());
    }
}
