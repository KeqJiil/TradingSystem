namespace Stock.Application.Abstractions;

public interface IStockDailyReadModelWriter
{
    Task CreateDailyReadModelAsync(DailyReadModelAggregate aggregate, CancellationToken ct);
}

public readonly record struct DailyReadModelAggregate(
    Guid AggregateId,
    DateOnly Date,
    decimal OpenPrice,
    decimal LowPrice,
    decimal HighPrice,
    decimal ClosePrice,
    decimal PriceDifference,
    long LastVersion);