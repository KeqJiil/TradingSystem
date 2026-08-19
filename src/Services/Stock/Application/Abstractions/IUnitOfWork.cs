namespace Stock.Application.Abstractions;

public interface IUnitOfWork : IAsyncDisposable
{
    public Task StartTransactionAsync(CancellationToken ct);
    public ValueTask CommitAsync(CancellationToken ct);
    public ValueTask RollbackAsync(CancellationToken ct);
}

public interface IUnitOfWorkDecorator
{
    public Task ExecuteAsync(Func<Task> action, CancellationToken ct);
}
