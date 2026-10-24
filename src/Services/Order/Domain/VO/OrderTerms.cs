using Order.Domain.Aggregates;

namespace Order.Domain.VO;

public record OrderTerms(TimeInForce TimeInForce, OrderType Type, Price? LimitPrice = null)
{
    public static Result<OrderTerms> Create(TimeInForce timeInForce, OrderType type, Price? limitPrice = null)
    {
        if (!Enum.IsDefined(timeInForce) || !Enum.IsDefined(type))
            return Result<OrderTerms>.Failure(new Error
                { Message = "Invalid order terms.", Type = ErrorType.Validation });

        if (type == OrderType.Market && (timeInForce == TimeInForce.Day || timeInForce == TimeInForce.Gtc))
            return Result<OrderTerms>.Failure(new Error
                { Message = "Market orders cannot have TimeInForce set to Day or Gtc.", Type = ErrorType.Validation });

        if (type == OrderType.Limit && (limitPrice == null || limitPrice.Value <= 0))
            return Result<OrderTerms>.Failure(new Error
                { Message = "Limit price must be greater than zero for limit orders.", Type = ErrorType.Validation });

        if (type == OrderType.Market && limitPrice != null)
            return Result<OrderTerms>.Failure(new Error
                { Message = "Limit price should not be set for market orders.", Type = ErrorType.Validation });

        return Result<OrderTerms>.Success(new OrderTerms(timeInForce, type, limitPrice));
    }
};