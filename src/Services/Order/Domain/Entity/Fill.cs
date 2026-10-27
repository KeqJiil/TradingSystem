using Order.Domain.VO;

namespace Order.Domain.Entity;

public record Fill(Guid ExecutionId, Quantity Quantity, Guid OrderId, Price Price, DateTimeOffset FilledAt)
{
    public static Result<Fill> Create(Guid executionId, Guid orderId, Quantity quantity, Price price,
        DateTimeOffset filledAt)
    {
        if (executionId == Guid.Empty)
            return Result<Fill>.Failure(new Error
                { Message = "ExecutionId cannot be empty.", Type = ErrorType.Validation });

        var fill = new Fill(executionId, quantity, orderId, price, filledAt);

        return Result<Fill>.Success(fill);
    }
}