namespace Stock.Application.Abstractions;

public interface IStockReadModelWriter
{
    Task<ReadModelUpdateOutcome> UpdateAsync(Guid aggregateId, long version, decimal priceChange, CancellationToken ct);

    Task<bool> CreateAsync(CreateStockReadModelDto data, CancellationToken ct);

    Task<bool> ToggleStatusAsync(Guid aggregateId, CancellationToken ct);
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