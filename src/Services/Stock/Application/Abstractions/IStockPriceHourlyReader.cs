namespace Stock.Application.Abstractions;

public interface IStockPriceHourlyReader
{
    public Task<PriceHourReadModel?> GetHourPriceHistoryAsync(Guid stockId, DateTimeOffset dateTime,
        CancellationToken ct);

    public Task<IEnumerable<PriceHourReadModel>> GetHourlyPriceHistoryAsync(Guid stockId, DateTimeOffset from,
        DateTimeOffset to, CancellationToken ct);

    public Task<PriceHourReadModel?> GetLastHourPriceHistoryAsync(Guid stockId, DateTimeOffset before,
        CancellationToken ct);
}

public record struct PriceHourReadModel(
    Guid StockId,
    decimal OpenPrice,
    decimal LowPrice,
    decimal HighPrice,
    decimal ClosePrice,
    decimal Difference,
    DateTimeOffset DateTime);