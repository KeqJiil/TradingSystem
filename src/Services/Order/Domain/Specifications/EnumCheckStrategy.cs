using Order.Domain.Aggregates;

namespace Order.Domain.Specifications;

public static class EnumCheckSpecification
{
    public static Result ValidateEnums(OrderSide side, TimeInForce tif, OrderType type)
    {
        if (!Enum.IsDefined(side) || !Enum.IsDefined(tif) || !Enum.IsDefined(type))
            return Result.Failure(new Error { Message = "Invalid OrderStatus enum value." });

        return Result.Success();
    }
}