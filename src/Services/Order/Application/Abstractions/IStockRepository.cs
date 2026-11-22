namespace Order.Application.Abstractions;

public interface IStockRepository
{
    Task<Domain.Aggregates.Stock?> GetByIdAsync(Guid stockId, CancellationToken cancellationToken = default);
    void Add(Domain.Aggregates.Stock stock);
}
