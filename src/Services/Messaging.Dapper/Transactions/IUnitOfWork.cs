namespace Messaging.Dapper.Transactions;

public interface IUnitOfWork : IAsyncDisposable
{
    ValueTask<bool> StartTransactionAsync(CancellationToken ct);
    ValueTask CommitAsync(CancellationToken ct);
    ValueTask RollbackAsync(CancellationToken ct);
}

public interface IUnitOfWorkDecorator
{
    Task ExecuteAsync(Func<Task> action, CancellationToken ct);
}
