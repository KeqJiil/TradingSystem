using System.Data.Common;

namespace Messaging.Dapper.DbTasks;

public delegate Task<bool> DbTask(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken);

public interface IDbTaskQueue
{
    void Register(string key, DbTask task);
}

public interface ITransactionHook
{
    Task<bool> OnCommittingAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);
    void OnCommitted();
    void OnRolledBack();
}

internal sealed class DbTaskQueue : IDbTaskQueue, ITransactionHook
{
    private enum State { Pending, Executed, Committed }

    private sealed class Entry(string key, DbTask task)
    {
        public string Key { get; } = key;
        public DbTask Task { get; } = task;
        public State State { get; set; }
    }

    private readonly List<Entry> _entries = new List<Entry>();

    public bool Vetoed { get; private set; }
    public bool HasPending => _entries.Exists(e => e.State == State.Pending);

    public void Register(string key, DbTask task)
    {
        if (_entries.Exists(e => e.Key == key)) return;
        _entries.Add(new Entry(key, task));
    }

    public async Task<bool> RunPendingAsync(DbConnection connection, DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry.State != State.Pending) continue;

            if (!await entry.Task(connection, transaction, cancellationToken))
            {
                Vetoed = true;
                return false;
            }

            entry.State = transaction is null ? State.Committed : State.Executed;
        }

        return true;
    }

    public Task<bool> OnCommittingAsync(DbConnection connection, DbTransaction transaction,
        CancellationToken cancellationToken) => RunPendingAsync(connection, transaction, cancellationToken);

    public void OnCommitted()
    {
        foreach (var entry in _entries.Where(e => e.State == State.Executed))
            entry.State = State.Committed;
    }

    public void OnRolledBack()
    {
        foreach (var entry in _entries.Where(e => e.State == State.Executed))
            entry.State = State.Pending;
    }
}
