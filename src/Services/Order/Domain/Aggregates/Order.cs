using Order.Domain.Entity;
using Order.Domain.Events;
using Order.Domain.VO;

namespace Order.Domain.Aggregates;

public class Order : AggregateRoot
{
    private readonly List<Fill> _fills = new();
    
    public override Guid Id { get; protected init; }
    public Guid UserId { get; private init; }

    public Symbols Symbol { get; private init; }
    public Quantity Quantity { get; private init; }
    
    public IReadOnlyList<Fill> Fills => _fills.AsReadOnly();
    public int FilledQuantity => _fills.Sum(f => f.Quantity.Value);
    public int Remaining => Quantity.Value - FilledQuantity;

    public OrderSide OrderSide { get; private init; }
    public OrderTerms OrderTerms { get; private init; }
    public OrderStatus Status { get; private set; }
    
    public string? CancellationReason { get; private set; }
    public DateTimeOffset? CanceledAt { get; private set; }

    public string? RejectionReason { get; private set; }
    public DateTimeOffset? RejectedAt { get; private set; }
    
    public DateTimeOffset? FilledAt { get; private set; }
    public DateTimeOffset? ExpiredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }

    private Order()
    {
    }

    public static Result<Order> Create(Guid userId, Symbols symbol, Quantity quantity, OrderTerms orderTerms, OrderSide side,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(side))
            return Result<Order>.Failure(new Error
                { Message = "Invalid order side.", Type = ErrorType.Validation });
        
        var id = Guid.CreateVersion7();

        var order = new Order
        {
            Id = id,
            UserId = userId,
            Symbol = symbol,
            Quantity = quantity,
            OrderTerms = orderTerms,
            OrderSide = side,
            Status = OrderStatus.PendingNew,
            CreatedAt = now
        };

        order.AddDomainEvent(new OrderPlaced(id, userId, symbol, side, quantity, orderTerms, now));

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