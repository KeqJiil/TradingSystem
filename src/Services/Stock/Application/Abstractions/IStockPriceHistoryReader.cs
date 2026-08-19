namespace Stock.Application.Abstractions;

public interface IStockPriceHistoryReader
{
    public Task<IEnumerable<PriceHistoryReadModel>> GetPriceHistoryAsync(Guid stockId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    public Task<IEnumerable<PriceHistoryDateOnlyReadModel>> GetDailyPriceHistoryAsync(Guid stockId, DateOnly from, DateOnly to, CancellationToken ct);
}

public struct PriceHistoryReadModel(decimal CurrentPrice, decimal Difference, DateTimeOffset Date);
public struct PriceHistoryDateOnlyReadModel(decimal CurrentPrice, decimal Difference, DateOnly Date);