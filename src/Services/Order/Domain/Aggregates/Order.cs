using Order.Domain.Entity;
using Order.Domain.Events;
using Order.Domain.Specifications;
using Order.Domain.VO;

namespace Order.Domain.Aggregates;

public class Order : AggregateRoot
{
    private static readonly HashSet<OrderStatus> _terminalStatuses =
        [OrderStatus.Canceled, OrderStatus.Filled, OrderStatus.Expired, OrderStatus.Rejected];

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

    public DateTimeOffset? ApprovedAt { get; private set; }
    public DateTimeOffset? FilledAt { get; private set; }
    public DateTimeOffset? ExpiredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }

    private Order()
    {
    }

    public static Result<Order> Create(Guid userId, Symbols symbol, Quantity quantity, OrderTerms orderTerms,
        OrderSide side,
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

    public Result Approve(DateTimeOffset now)
    {
        if (Status == OrderStatus.Rejected)
            return Result.Failure(new Error
                { Message = "Rejected order cannot be accepted.", Type = ErrorType.Violation });

        if (Status == OrderStatus.PendingNew)
        {
            Status = OrderStatus.New;
            ApprovedAt = now;
            AddDomainEvent(new OrderApproved(Id, now));
            return Result.Success();
        }

        if (Status == OrderStatus.PendingCancel && ApprovedAt == null)
        {
            ApprovedAt = now;
            AddDomainEvent(new OrderApproved(Id, now));
        }

        return Result.Success();
    }

    public Result Reject(string reason, DateTimeOffset now)
    {
        if (Status == OrderStatus.Rejected)
            return Result.Success();

        if (Status != OrderStatus.PendingNew && !(Status == OrderStatus.PendingCancel && ApprovedAt == null))
            return Result.Failure(new Error
            {
                Message = "Order can only be rejected from PendingNew or unapproved PendingCancel status.",
                Type = ErrorType.Violation
            });

        Status = OrderStatus.Rejected;
        RejectionReason = reason;
        RejectedAt = now;

        AddDomainEvent(new OrderRejected(Id, reason, now));

        return Result.Success();
    }

    public Result ApplyFill(Guid executionId, Quantity quantity, Price price, DateTimeOffset filledAt)
    {
        if (Fills.Any(f => f.ExecutionId == executionId))
            return Result.Success();

        var specificationResult = ApplyFillSpecification.Validate(OrderSide, price,
            OrderTerms.LimitPrice, quantity, Remaining, Status, ApprovedAt != null);

        if (specificationResult.IsFailure)
            return Result.Failure(specificationResult.Error);

        var result = Fill.Create(executionId, Id, quantity, price, filledAt);
        if (result.IsFailure)
            return Result.Failure(result.Error);

        _fills.Add(result.Value);

        if (Remaining == 0)
            Status = OrderStatus.Filled;
        else if (Status != OrderStatus.PendingCancel)
            Status = OrderStatus.PartiallyFilled;

        AddDomainEvent(new FillRecorded(Id, executionId, quantity, price, filledAt));

        if (Status == OrderStatus.Filled)
        {
            AddDomainEvent(new OrderFilled(Id, FilledQuantity, filledAt));
            FilledAt = filledAt;
        }

        return Result.Success();
    }

    public Result RequestCancel(DateTimeOffset now)
    {
        if (Status == OrderStatus.Canceled || Status == OrderStatus.PendingCancel)
            return Result.Success();

        if (Status != OrderStatus.New && Status != OrderStatus.PartiallyFilled && Status != OrderStatus.PendingNew)
            return Result.Failure(new Error
            {
                Message = "Order can only request cancel from New or PartiallyFilled status.",
                Type = ErrorType.Conflict
            });

        Status = OrderStatus.PendingCancel;

        AddDomainEvent(new OrderCancelRequested(Id, now));

        return Result.Success();
    }

    public Result Cancel(string reason, DateTimeOffset now)
    {
        if (Status == OrderStatus.Canceled)
            return Result.Success();

        if (Status != OrderStatus.New && Status != OrderStatus.PartiallyFilled && Status != OrderStatus.PendingCancel)
            return Result.Failure(new Error
            {
                Message = "Order can only be canceled from New or PartiallyFilled status.", Type = ErrorType.Violation
            });

        Status = OrderStatus.Canceled;
        CancellationReason = reason;
        CanceledAt = now;

        AddDomainEvent(new OrderCanceled(Id, reason, now));

        return Result.Success();
    }

    public Result RejectCancel(string reason, DateTimeOffset now)
    {
        if (_terminalStatuses.Contains(Status))
            return Result.Success();

        if (Status != OrderStatus.PendingCancel)
            return Result.Failure(new Error
                { Message = "Order can only reject cancel from PendingCancel status.", Type = ErrorType.Violation });

        if (ApprovedAt == null)
            Status = OrderStatus.PendingNew;
        else
            Status = FilledQuantity == 0 ? OrderStatus.New : OrderStatus.PartiallyFilled;

        AddDomainEvent(new OrderCancelRejected(Id, reason, now));

        return Result.Success();
    }

    public Result Expire(DateTimeOffset now)
    {
        if (Status == OrderStatus.Expired)
            return Result.Success();

        if (Status != OrderStatus.New && Status != OrderStatus.PartiallyFilled && Status != OrderStatus.PendingCancel)
            return Result.Failure(new Error
            {
                Message = "Order can only be expired from New or PartiallyFilled status.", Type = ErrorType.Violation
            });

        Status = OrderStatus.Expired;
        ExpiredAt = now;

        AddDomainEvent(new OrderExpired(Id, now));

        return Result.Success();
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