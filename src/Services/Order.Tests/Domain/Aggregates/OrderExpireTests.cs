using Order.Domain;
using Order.Domain.Aggregates;
using Order.Domain.Events;
using Xunit;
using static Order.Tests.Domain.Aggregates.TestOrders;

namespace Order.Tests.Domain.Aggregates;

public class OrderExpireTests
{
    [Fact]
    public void Expire_AlreadyExpired_DoesNothing()
    {
        var order = InStatus(OrderStatus.Expired);

        var result = order.Expire(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Expired, order.Status);
        Assert.Equal(ApprovedAt, order.ExpiredAt);
        Assert.Empty(order.GetDomainEvents());
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.PartiallyFilled)]
    [InlineData(OrderStatus.PendingCancel)]
    public void Expire_ExpirableStatus_ExpiresAndRaisesOrderExpired(OrderStatus status)
    {
        var order = InStatus(status);

        var result = order.Expire(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Expired, order.Status);
        Assert.Equal(Now, order.ExpiredAt);
        var expired = Assert.IsType<OrderExpired>(Assert.Single(order.GetDomainEvents()));
        Assert.Equal(order.Id, expired.OrderId);
        Assert.Equal(Now, expired.ExpiredAt);
    }

    [Theory]
    [InlineData(OrderStatus.PendingNew)]
    [InlineData(OrderStatus.Rejected)]
    [InlineData(OrderStatus.Filled)]
    [InlineData(OrderStatus.Canceled)]
    public void Expire_NotExpirableStatus_ReturnsViolation(OrderStatus status)
    {
        var order = InStatus(status);

        var result = order.Expire(Now);

        AssertFailure(result, ErrorType.Violation, "Order can only be expired from New or PartiallyFilled status.");
        Assert.Equal(status, order.Status);
        Assert.Null(order.ExpiredAt);
        Assert.Empty(order.GetDomainEvents());
    }
}
