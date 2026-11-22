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
}
