namespace Stock.Application.Abstractions;

public interface IStockPriceHistoryReader
{
    public Task<PriceHistoryDateOnlyReadModel?> GetDayPriceHistoryAsync(Guid stockId, DateOnly date, CancellationToken ct);
    public Task<IEnumerable<PriceHistoryReadModel>> GetPriceHistoryAsync(Guid stockId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    public Task<IEnumerable<PriceHistoryDateOnlyReadModel>> GetDailyPriceHistoryAsync(Guid stockId, DateOnly from, DateOnly to, CancellationToken ct);
}

public record PriceHistoryReadModel(decimal CurrentPrice, decimal Difference, DateTimeOffset Date);
public readonly record struct PriceHistoryDateOnlyReadModel(decimal OpenPrice, decimal LowPrice, decimal HighPrice, decimal ClosePrice, decimal Difference, DateOnly Date);