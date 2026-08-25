namespace Stock.Application.Abstractions;

public interface IStockReadModelWriter
{
    Task<bool> UpdateAsync(Guid aggregateId, long version, decimal priceChange, CancellationToken ct);

    Task<bool> CreateAsync(CreateStockReadModelDto data, CancellationToken ct);

    Task<bool> ToggleStatusAsync(Guid aggregateId, CancellationToken ct);
}

public record CreateStockReadModelDto(Guid aggregateId, string name,
    bool isOpenToTrade, string currency,
    TimeOnly tradingStartTime,
    TimeOnly tradingCloseTime);