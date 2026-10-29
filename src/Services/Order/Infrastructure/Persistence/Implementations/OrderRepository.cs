using Microsoft.EntityFrameworkCore;
using Order.Application.Abstractions;
using Order.Infrastructure.Persistence.Configuration;

namespace Order.Infrastructure.Persistence.Implementations;

public class OrderRepository(OrderDbContext dbContext) : IOrderRepository
{
    public Task<Domain.Aggregates.Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return dbContext.Orders
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
    }

    public void Add(Domain.Aggregates.Order order)
    {
        dbContext.Add(order);
    }
}