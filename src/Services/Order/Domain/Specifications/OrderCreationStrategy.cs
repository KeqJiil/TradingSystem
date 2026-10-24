using Order.Domain.Aggregates;

namespace Order.Domain.Specifications;

public static class OrderCreationSpecification
{
    public static Result CanCreate(decimal? limitPrice, int quantity, OrderType type)
    {
        if (quantity <= 0) return Result.Failure(new Error { Message = "Quantity must be greater than zero." });

        if (type == OrderType.Limit && (!limitPrice.HasValue || limitPrice <= 0))
            return Result.Failure(new Error
                { Message = "Limit price must be greater than zero for limit orders." });

        if (limitPrice.HasValue && limitPrice <= 0)
            return Result.Failure(new Error { Message = "Limit price must be greater than zero." });

        if (type == OrderType.Market && limitPrice.HasValue)
            return Result.Failure(new Error { Message = "Limit price should not be set for market orders." });

        return Result.Success();
    }
}