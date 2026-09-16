using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockPriceHourlyReader(IDbContext dbContext) : IStockPriceHourlyReader
{
    public Task<PriceHistoryReadModel?> GetHourPriceHistoryAsync(Guid stockId, DateTimeOffset dateTime,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<PriceHistoryReadModel>> GetHourlyPriceHistoryAsync(Guid stockId, DateTimeOffset from,
        DateTimeOffset to, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<PriceHistoryReadModel?> GetLastHourPriceHistoryAsync(Guid stockId, DateTimeOffset before,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}