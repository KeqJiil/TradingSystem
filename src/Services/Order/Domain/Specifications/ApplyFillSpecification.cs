using Order.Domain.Aggregates;
using Order.Domain.VO;

namespace Order.Domain.Specifications;

public static class ApplyFillSpecification
{
    public static Result Validate(OrderSide orderSide, Price price, Price? limitPrice, Quantity quantity, int remaining, OrderStatus status, bool isApproved)
    {
        if (!isApproved)
            return Result.Failure(new Error
                { Message = "Order must be approved before it can be filled.", Type = ErrorType.Violation });

        if (orderSide == OrderSide.Sell && limitPrice != null && price.Value < limitPrice.Value)
            return Result.Failure(new Error
                { Message = "Fill price is below limit price.", Type = ErrorType.Violation });

        if (orderSide == OrderSide.Buy && limitPrice != null && price.Value > limitPrice.Value)
            return Result.Failure(new Error
                { Message = "Fill price exceeds limit price.", Type = ErrorType.Violation });

        if (remaining < quantity.Value)
            return Result.Failure(new Error
                { Message = "Fill quantity exceeds remaining order quantity.", Type = ErrorType.Violation });

        if (status != OrderStatus.New && status != OrderStatus.PartiallyFilled && status != OrderStatus.PendingCancel)
            return Result.Failure(new Error
            {
                Message = "Order can only be filled from New or PartiallyFilled status.", Type = ErrorType.Violation
            });

        if (quantity.Value > remaining)
            return Result.Failure(new Error
                { Message = "Fill quantity exceeds remaining order quantity.", Type = ErrorType.Violation });

        return Result.Success();
    }
}