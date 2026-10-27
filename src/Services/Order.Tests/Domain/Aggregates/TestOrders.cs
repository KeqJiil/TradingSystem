using Order.Domain;
using Order.Domain.Aggregates;
using Order.Domain.VO;
using Xunit;
using OrderAggregate = Order.Domain.Aggregates.Order;

namespace Order.Tests.Domain.Aggregates;

public static class TestOrders
{
    public static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset ApprovedAt = CreatedAt.AddMinutes(1);
    public static readonly DateTimeOffset Now = ApprovedAt.AddMinutes(5);
    public const int Quantity = 10;
    public const string Reason = "test";

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

    public static OrderAggregate PartiallyFilledPendingCancel()
    {
        var order = InStatus(OrderStatus.PartiallyFilled);
        Assert.True(order.RequestCancel(ApprovedAt).IsSuccess);
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
            OrderStatus.PendingNew or OrderStatus.New => Result.Success(),
            OrderStatus.Rejected => order.Reject(Reason, ApprovedAt),
            OrderStatus.PartiallyFilled => Fill(order, Guid.NewGuid(), 1, ApprovedAt),
            OrderStatus.Filled => Fill(order, Guid.NewGuid(), Quantity, ApprovedAt),
            OrderStatus.PendingCancel => order.RequestCancel(ApprovedAt),
            OrderStatus.Canceled => order.Cancel(Reason, ApprovedAt),
            OrderStatus.Expired => order.Expire(ApprovedAt),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        Assert.True(result.IsSuccess);
        Assert.Equal(status, order.Status);
        order.ClearDomainEvents();
        return order;
    }

    public static Result Fill(OrderAggregate order, Guid executionId, int quantity, DateTimeOffset filledAt)
    {
        return order.ApplyFill(executionId, Order.Domain.VO.Quantity.Create(quantity).Value,
            Price.Create(10m).Value, filledAt);
    }

    public static void AssertFailure(Result result, ErrorType type, string message)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(type, result.Error.Type);
        Assert.Equal(message, result.Error.Message);
    }
}
