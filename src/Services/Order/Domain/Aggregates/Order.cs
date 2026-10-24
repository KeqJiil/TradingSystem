using Order.Domain.Events;
using Order.Domain.Specifications;

namespace Order.Domain.Aggregates;

public class Order : AggregateRoot
{
    public override Guid Id { get; protected init; }
    public Guid UserId { get; private init; }

    public Symbols Symbol { get; private init; }
    public int Quantity { get; private init; }
    public decimal? LimitPrice { get; private init; } = null;

    public TimeInForce TimeInForce { get; private init; }
    public OrderSide Side { get; private init; }
    public OrderType Type { get; private init; }
    public OrderStatus Status { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }

    private Order()
    {
    }

    public static Result<Order> Create(Guid userId, Symbols symbol, int quantity, decimal? limitPrice,
        TimeInForce timeInForce, OrderSide side, OrderType type, DateTimeOffset now)
    {
        var validationResult = OrderCreationSpecification.CanCreate(limitPrice, quantity, type);
        var enumValidationResult = EnumCheckSpecification.ValidateEnums(side, timeInForce, type);

        if (validationResult.IsFailure)
            return Result<Order>.Failure(validationResult.Error);

        if (enumValidationResult.IsFailure)
            return Result<Order>.Failure(enumValidationResult.Error);

        var id = Guid.NewGuid();

        var order = new Order
        {
            Id = id,
            UserId = userId,
            Symbol = symbol,
            Quantity = quantity,
            LimitPrice = limitPrice,
            TimeInForce = timeInForce,
            Side = side,
            Type = type,
            Status = OrderStatus.PendingNew,
            CreatedAt = now
        };

        order.AddDomainEvent(new NewOrderPendingEvent(id, symbol, quantity, limitPrice, timeInForce, side, type,
            now));

        return Result<Order>.Success(order);
    }
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

public enum OrderSide
{
    Buy = 1,
    Sell = 2
}

public enum OrderType
{
    Market = 1, // execute at the best available price
    Limit = 2 // execute at a specified price or better
}

public enum TimeInForce
{
    Day = 1, // until the end of the trading day
    Gtc = 2, // until canceled
    Ioc = 3, // immediate or cancel
    Fok = 4 // fill or kill
}

public enum Symbols
{
}