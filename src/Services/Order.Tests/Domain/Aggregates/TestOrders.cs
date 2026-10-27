using Order.Domain.Aggregates;
using Order.Domain.VO;
using Xunit;
using OrderAggregate = Order.Domain.Aggregates.Order;

namespace Order.Tests.Domain.Aggregates;

public static class TestOrders
{
    public static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset ApprovedAt = CreatedAt.AddMinutes(1);
    public const int Quantity = 10;

    public static OrderAggregate PendingNew()
    {
        var terms = OrderTerms.Create(TimeInForce.Ioc, OrderType.Market).Value;
        var quantity = Order.Domain.VO.Quantity.Create(Quantity).Value;

        return OrderAggregate.Create(Guid.NewGuid(), default, quantity, terms, OrderSide.Buy, CreatedAt).Value;
    }

    public static OrderAggregate UnapprovedPendingCancel()
    {
        var order = PendingNew();
        Assert.True(order.RequestCancel(CreatedAt).IsSuccess);
        order.ClearDomainEvents();
        return order;
    }

    public static OrderAggregate InStatus(OrderStatus status)
    {
        var order = PendingNew();

        if (status != OrderStatus.PendingNew && status != OrderStatus.Rejected)
            Assert.True(order.Approve(ApprovedAt).IsSuccess);

        var result = status switch
        {
            OrderStatus.PendingNew or OrderStatus.New => Order.Domain.Result.Success(),
            OrderStatus.Rejected => order.Reject("test", ApprovedAt),
            OrderStatus.PartiallyFilled => Fill(order, 1),
            OrderStatus.Filled => Fill(order, Quantity),
            OrderStatus.PendingCancel => order.RequestCancel(ApprovedAt),
            OrderStatus.Canceled => order.Cancel("test", ApprovedAt),
            OrderStatus.Expired => order.Expire(ApprovedAt),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        Assert.True(result.IsSuccess);
        Assert.Equal(status, order.Status);
        order.ClearDomainEvents();
        return order;
    }

    private static Order.Domain.Result Fill(OrderAggregate order, int quantity)
    {
        return order.ApplyFill(Guid.NewGuid(), Order.Domain.VO.Quantity.Create(quantity).Value,
            Price.Create(10m).Value, ApprovedAt);
    }
}
