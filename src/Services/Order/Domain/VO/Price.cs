namespace Order.Domain.VO;

public record Price(decimal? Value)
{
    public static Result<Price> Create(decimal value)
    {
        if (value <= 0)
            return Result<Price>.Failure(new Error
                { Message = "Price must be greater than zero.", Type = ErrorType.Validation });

        if (value % 0.01m != 0)
            return Result<Price>.Failure(new Error
                { Message = "Price must be a multiple of 0.01.", Type = ErrorType.Validation });

        return Result<Price>.Success(new Price(value));
    }
}