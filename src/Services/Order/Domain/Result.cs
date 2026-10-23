namespace Order.Domain;

public readonly struct Result<T>
{
    public bool IsSuccess { get; private init; }
    public T Value { get => IsSuccess ? field : throw new InvalidOperationException("No value present"); init; }
    public Error Error { get => !IsSuccess ? field : throw new InvalidOperationException("No error present"); init; }

    public static Result<T> Success(T value) => new Result<T> { IsSuccess = true, Value = value };
    public static Result<T> Failure(Error error) => new Result<T> { IsSuccess = false, Error = error };
}

public readonly struct Result
{
    public bool IsSuccess { get; private init; }
    public Error Error { get => !IsSuccess ? field : throw new InvalidOperationException("No error present"); init; }

    public static Result Success() => new Result { IsSuccess = true };
    public static Result Failure(Error error) => new Result { IsSuccess = false, Error = error };
}

public readonly struct Error
{
    public string Message { get; init; }
}