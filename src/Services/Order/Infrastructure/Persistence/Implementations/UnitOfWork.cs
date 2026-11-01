using Order.Application.Abstractions;
using Order.Infrastructure.Persistence.Configuration;

namespace Order.Infrastructure.Persistence.Implementations;

public class UnitOfWork(OrderDbContext dbContext) : IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}