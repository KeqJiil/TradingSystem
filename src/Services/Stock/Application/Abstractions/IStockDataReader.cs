namespace Stock.Application.Abstractions;

public interface IStockDataReader
{
    public IAsyncEnumerable<Guid> GetAllIdsAsync(int limit, CancellationToken cancellationToken = default);

    public Task<StockMetadata?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public readonly record struct StockMetadata(
    Guid Id,
    string Name,
    bool IsOpenToTrade,
    TimeOnly? TradingStartTime,
    TimeOnly? TradingEndTime,
    string? Currency,
    DateTimeOffset CreatedAt);