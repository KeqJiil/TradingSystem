namespace Order.Domain.Aggregates;

public class Order : AggregateRoot
{
    public Guid UserId { get; private init; }

    public OrderStatus Status { get; private set; }
}

public enum OrderStatus
{
    PendingNew = 1,
    New = 2,
    Rejected = 3,
    PartiallyFilled = 4,
    Filled = 5,
    PendingCancel = 6,
    Canceled = 7,
    Expired = 8
}

public enum OrderSide { Buy = 1, Sell = 2 }
public enum OrderType { Market = 1, Limit = 2 }
public enum TimeInForce { Day = 1, Gtc = 2, Ioc = 3, Fok = 4 }