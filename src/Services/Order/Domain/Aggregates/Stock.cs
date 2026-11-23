namespace Order.Domain.Aggregates;

public class Stock
{
    public Guid Id { get; private init; }
    public string Name { get; private set; } = null!;
    public bool IsOpenToTrade { get; private set; }
    public TimeOnly TradingStart { get; private set; }
    public TimeOnly TradingEnd { get; private set; }

    public long NameVersion { get; private set; }
    public long StatusVersion { get; private set; }
    public long TradingTimeVersion { get; private set; }

    private Stock()
    {
    }

    public static Result<Stock> Create(Guid id, string name, bool isOpenToTrade, TimeOnly tradingStart,
        TimeOnly tradingEnd)
    {
        if (id == Guid.Empty)
            return Result<Stock>.Failure(new Error
                { Message = "Invalid stock id.", Type = ErrorType.Validation });

        if (string.IsNullOrWhiteSpace(name))
            return Result<Stock>.Failure(new Error
                { Message = "Invalid stock name.", Type = ErrorType.Validation });

        return Result<Stock>.Success(new Stock
        {
            Id = id,
            Name = name,
            IsOpenToTrade = isOpenToTrade,
            TradingStart = tradingStart,
            TradingEnd = tradingEnd
        });
    }

    public Result Rename(string name, long version)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(new Error
                { Message = "Invalid stock name.", Type = ErrorType.Validation });

        if (version <= NameVersion)
            return Result.Success();

        Name = name;
        NameVersion = version;
        return Result.Success();
    }

    public Result SetStatus(bool isOpenToTrade, long version)
    {
        if (version <= StatusVersion)
            return Result.Success();

        IsOpenToTrade = isOpenToTrade;
        StatusVersion = version;
        return Result.Success();
    }

    public Result SetTradingTime(TimeOnly tradingStart, TimeOnly tradingEnd, long version)
    {
        if (version <= TradingTimeVersion)
            return Result.Success();

        TradingStart = tradingStart;
        TradingEnd = tradingEnd;
        TradingTimeVersion = version;
        return Result.Success();
    }
}
