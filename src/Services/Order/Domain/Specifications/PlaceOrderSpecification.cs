namespace Order.Domain.Specifications;

public static class PlaceOrderSpecification
{
    public static Result Validate(bool isOpenToTrade, TimeOnly tradingStart, TimeOnly tradingEnd, TimeOnly now)
    {
        if (!isOpenToTrade)
            return Result.Failure(new Error
                { Message = "Stock is not open to trade.", Type = ErrorType.Violation });

        var isInsideTradingHours = tradingStart <= tradingEnd
            ? now >= tradingStart && now < tradingEnd
            : now >= tradingStart || now < tradingEnd;

        if (!isInsideTradingHours)
            return Result.Failure(new Error
                { Message = "Stock is outside trading hours.", Type = ErrorType.Violation });

        return Result.Success();
    }
}
