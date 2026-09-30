namespace Stock.Application.Abstractions;

public interface IStockReadModelWriter
{
    Task<ReadModelUpdateOutcome> UpdateAsync(Guid aggregateId, long version, decimal priceChange, CancellationToken ct);

    Task<bool> ReplayAsync(Guid aggregateId, long fromVersion, long toVersion, decimal priceChange,
        CancellationToken ct);

    Task<bool> CreateAsync(CreateStockReadModelDto data, CancellationToken ct);

    Task<bool> SetStatusAsync(Guid aggregateId, bool isOpenToTrade, long statusVersion, CancellationToken ct);

    Task<bool> SetNewTimeAsync(Guid aggregateId, TimeOnly tradingStartTime, TimeOnly tradingCloseTime,
        long timeVersion, CancellationToken ct);

    Task<bool> SetNewNameAsync(Guid aggregateId, string newName, long nameVersion, CancellationToken ct);
}

public enum ReadModelUpdateOutcome
{
    Applied,
    Gap,
    Stale
}

public record CreateStockReadModelDto(
    Guid AggregateId,
    string Name,
    bool IsOpenToTrade,
    string Currency,
    TimeOnly TradingStartTime,
    TimeOnly TradingCloseTime);