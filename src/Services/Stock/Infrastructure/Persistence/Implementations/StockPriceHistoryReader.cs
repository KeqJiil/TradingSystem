using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockPriceHistoryReader(IUnitOfWork unitOfWork) : IStockPriceHistoryReader
{
    public Task<IEnumerable<PriceHistoryDateOnlyReadModel>> GetDailyPriceHistoryAsync(Guid stockId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<PriceHistoryReadModel>> GetPriceHistoryAsync(Guid stockId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}