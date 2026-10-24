using Order.Domain.VO;

namespace Order.Domain.Entity;

public record Fill(
    Guid ExecutionId,
    Quantity Quantity,
    Guid OrderId,
    Price Price,
    DateTimeOffset FilledAt)
{
    public static Result<Fill> Create(Guid executionId, Guid orderId, Quantity quantity, Price price, DateTimeOffset filledAt)
    {
        var fill = new Fill(executionId, quantity, orderId, price, filledAt);

        return Result<Fill>.Success(fill);
    }
}