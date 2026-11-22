using Microsoft.EntityFrameworkCore;
using Order.Application.Abstractions;
using Order.Infrastructure.Persistence.Configuration;

namespace Order.Infrastructure.Persistence.Implementations;

public class StockRepository(OrderDbContext dbContext) : IStockRepository
{
    public Task<Domain.Aggregates.Stock?> GetByIdAsync(Guid stockId, CancellationToken cancellationToken = default)
    {
        return dbContext.Stocks
            .FirstOrDefaultAsync(x => x.Id == stockId, cancellationToken);
    }

    public void Add(Domain.Aggregates.Stock stock)
    {
        dbContext.Add(stock);
    }
}
