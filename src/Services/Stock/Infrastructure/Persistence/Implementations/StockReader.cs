using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockReader : IStockReader
{
    public Task<StockReadModel> GetByIdAsync(Guid stockId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}