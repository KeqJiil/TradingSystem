namespace Order.Domain.VO;

public record struct Quantity(int Value)
{
    public static Result<Quantity> Create(int value)
    {
        if (value <= 0)
            return Result<Quantity>.Failure(new Error
                { Message = "Quantity must be greater than zero.", Type = ErrorType.Validation });

        return Result<Quantity>.Success(new Quantity(value));
    }
}