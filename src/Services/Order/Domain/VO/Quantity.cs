namespace Order.Domain.VO;

public record Quantity
{
    public int Value { get; }

    private Quantity(int value)
    {
        Value = value;
    }

    public static Result<Quantity> Create(int value)
    {
        if (value <= 0)
            return Result<Quantity>.Failure(new Error
                { Message = "Quantity must be greater than zero.", Type = ErrorType.Validation });

        return Result<Quantity>.Success(new Quantity(value));
    }
}
