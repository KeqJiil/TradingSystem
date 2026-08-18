namespace Stock.Application.Abstractions;

public interface IUnitOfWork : IDisposable
{
    public Task StartTransactionAsync(CancellationToken ct);
    public Task CommitAsync(CancellationToken ct);
    public Task RollbackAsync(CancellationToken ct);
}

