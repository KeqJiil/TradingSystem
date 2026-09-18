namespace Stock.Application.Abstractions;

public interface IStockHourlyReadModelWriter
{
    Task CreateHourlyReadModelAsync(HourlyReadModelAggregate aggregate, CancellationToken ct);
}

public readonly record struct HourlyReadModelAggregate(
    Guid AggregateId,
    DateTimeOffset HourStart,
    decimal OpenPrice,
    decimal LowPrice,
    decimal HighPrice,
    decimal ClosePrice,
    decimal PriceDifference,
    long LastVersion);