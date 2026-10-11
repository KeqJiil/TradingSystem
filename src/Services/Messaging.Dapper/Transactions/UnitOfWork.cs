using System.Data;
using System.Data.Common;
using Messaging.Dapper.DbTasks;

namespace Messaging.Dapper.Transactions;

internal sealed class UnitOfWork(DbConnection connection, IEnumerable<ITransactionHook> hooks)
    : IUnitOfWork, ITransactionContext
{
    private readonly ITransactionHook[] _hooks = hooks.ToArray();
    private bool _disposed;

    public DbConnection Connection { get; } = connection;
    public DbTransaction? Transaction { get; private set; }

    public async Task EnsureConnectionOpenAsync(CancellationToken cancellationToken)
    {
        if (Connection.State == ConnectionState.Closed) await Connection.OpenAsync(cancellationToken);
    }

    public async ValueTask<bool> StartTransactionAsync(CancellationToken ct)
    {
        if (Transaction is not null) return false;
        await EnsureConnectionOpenAsync(ct);
        Transaction = await Connection.BeginTransactionAsync(ct);
        return true;
    }

    public async ValueTask CommitAsync(CancellationToken ct)
    {
        if (Transaction is null) return;

        var transaction = Transaction;
        var committed = false;
        try
        {
            foreach (var hook in _hooks)
                if (!await hook.OnCommittingAsync(Connection, transaction, ct))
                    return;

            await transaction.CommitAsync(ct);
            committed = true;
        }
        finally
        {
            Transaction = null;
            await transaction.DisposeAsync();
            Notify(committed);
        }
    }

    public async ValueTask RollbackAsync(CancellationToken ct)
    {
        if (Transaction is null) return;

        var transaction = Transaction;
        try
        {
            await transaction.RollbackAsync(ct);
        }
        finally
        {
            Transaction = null;
            await transaction.DisposeAsync();
            Notify(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (Transaction is not null)
        {
            await Transaction.DisposeAsync();
            Transaction = null;
            Notify(false);
        }

        await Connection.CloseAsync();
        await Connection.DisposeAsync();
    }

    private void Notify(bool committed)
    {
        foreach (var hook in _hooks)
            if (committed) hook.OnCommitted();
            else hook.OnRolledBack();
    }
}
