namespace Order.Application.Abstractions;

public interface IOrderRepository
{
    Task<Domain.Aggregates.Order> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    void Add(Domain.Aggregates.Order order);
}