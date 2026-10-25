namespace Order.Domain;

public readonly struct Result<T>
{
    public bool IsSuccess { get; private init; }
    public bool IsFailure => !IsSuccess;

    public T Value
    {
        get => IsSuccess ? field : throw new InvalidOperationException("No value present");
        init;
    }

    public Error Error
    {
        get => !IsSuccess ? field : throw new InvalidOperationException("No error present");
        init;
    }

    public static Result<T> Success(T value)
    {
        return new Result<T> { IsSuccess = true, Value = value };
    }

    public static Result<T> Failure(Error error)
    {
        return new Result<T> { IsSuccess = false, Error = error };
    }
}

public readonly struct Result
{
    public bool IsSuccess { get; private init; }
    public bool IsFailure => !IsSuccess;

    public Error Error
    {
        get => !IsSuccess ? field : throw new InvalidOperationException("No error present");
        init;
    }

    public static Result Success()
    {
        return new Result { IsSuccess = true };
    }

    public static Result Failure(Error error)
    {
        return new Result { IsSuccess = false, Error = error };
    }
}

public readonly struct Error
{
    public string Message { get; init; }
    public ErrorType Type { get; init; }
}

public enum ErrorType
{
    Conflict,
    Validation,
    Violation
}