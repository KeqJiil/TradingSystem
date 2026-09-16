namespace Stock.Application.Abstractions;

public interface IStockPriceHourlyReader
{
    public Task<PriceHistoryReadModel?> GetHourPriceHistoryAsync(Guid stockId, DateTimeOffset dateTime,
        CancellationToken ct);

    public Task<IEnumerable<PriceHistoryReadModel>> GetHourlyPriceHistoryAsync(Guid stockId, DateTimeOffset from,
        DateTimeOffset to, CancellationToken ct);

    public Task<PriceHistoryReadModel?> GetLastHourPriceHistoryAsync(Guid stockId, DateTimeOffset before,
        CancellationToken ct);
}

public record struct PriceHistoryReadModel(
    Guid StockId,
    decimal OpenPrice,
    decimal LowPrice,
    decimal HighPrice,
    decimal ClosePrice,
    decimal Difference,
    DateTimeOffset DateTime);