using System.Data;
using System.Data.Common;
using Dapper;
using Messaging.Dapper.DbTasks;
using Messaging.Dapper.Tests.Integration.Infrastructure;
using Messaging.Dapper.Transactions;
using Microsoft.Data.SqlClient;

namespace Messaging.Dapper.Tests.Integration.Transactions;

public class UnitOfWorkTests(MssqlFixture db) : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly DbTaskQueue _queue = new();
    private SqlConnection _connection = null!;
    private UnitOfWork _sut = null!;

    public async Task InitializeAsync()
    {
        await using var connection = await db.OpenConnectionAsync();
        await connection.ExecuteAsync("""
                                      IF OBJECT_ID('dbo.uow_items', 'U') IS NULL
                                          CREATE TABLE uow_items (id VARCHAR(100) NOT NULL);
                                      """);

        _connection = new SqlConnection(db.ConnectionString);
        _sut = new UnitOfWork(_connection, [_queue]);
    }

    public async Task DisposeAsync()
    {
        await _sut.DisposeAsync();
    }

    [Fact]
    public async Task EnsureConnectionOpen_ClosedConnection_OpensIt()
    {
        Assert.Equal(ConnectionState.Closed, _sut.Connection.State);

        await _sut.EnsureConnectionOpenAsync(CancellationToken.None);

        Assert.Equal(ConnectionState.Open, _sut.Connection.State);
    }

    [Fact]
    public async Task StartTransaction_ClosedConnection_OpensItAndStartsTransaction()
    {
        var started = await _sut.StartTransactionAsync(CancellationToken.None);

        Assert.True(started);
        Assert.Equal(ConnectionState.Open, _sut.Connection.State);
        Assert.NotNull(_sut.Transaction);
    }

    [Fact]
    public async Task StartTransaction_Twice_ReturnsFalseAndKeepsFirstTransaction()
    {
        await _sut.StartTransactionAsync(CancellationToken.None);
        var first = _sut.Transaction;

        var started = await _sut.StartTransactionAsync(CancellationToken.None);

        Assert.False(started);
        Assert.Same(first, _sut.Transaction);
    }

    [Fact]
    public async Task Commit_HooksSucceed_RunsHookTasksInTransactionAndPersistsData()
    {
        var id = NewId();
        DbTransaction? hookTransaction = null;
        _queue.Register("a", (_, tx, _) =>
        {
            hookTransaction = tx;
            return Task.FromResult(true);
        });

        await _sut.StartTransactionAsync(CancellationToken.None);
        var transaction = _sut.Transaction;
        await InsertAsync(id);
        await _sut.CommitAsync(CancellationToken.None);

        Assert.Same(transaction, hookTransaction);
        Assert.True(_queue.IsCommitted("a"));
        Assert.Null(_sut.Transaction);
        Assert.Equal(1, await CountAsync(id));
    }

    [Fact]
    public async Task Commit_HookVetoes_RollsBackAndCallsRollbackHooks()
    {
        var id = NewId();
        _queue.Register("a", (_, _, _) => Task.FromResult(true));
        _queue.Register("b", (_, _, _) => Task.FromResult(false));

        await _sut.StartTransactionAsync(CancellationToken.None);
        await InsertAsync(id);
        await _sut.CommitAsync(CancellationToken.None);

        Assert.Null(_sut.Transaction);
        Assert.False(_queue.IsCommitted("a"));
        Assert.True(_queue.HasPending);
        Assert.Equal(0, await CountAsync(id));
    }

    [Fact]
    public async Task Rollback_AfterHookTasksRan_DiscardsDataAndCallsRollbackHooks()
    {
        var id = NewId();
        _queue.Register("a", (_, _, _) => Task.FromResult(true));

        await _sut.StartTransactionAsync(CancellationToken.None);
        await InsertAsync(id);
        await _queue.RunPendingAsync(_sut.Connection, _sut.Transaction, CancellationToken.None);
        await _sut.RollbackAsync(CancellationToken.None);

        Assert.Null(_sut.Transaction);
        Assert.True(_queue.HasPending);
        Assert.Equal(0, await CountAsync(id));
    }

    [Fact]
    public async Task Dispose_OpenTransaction_RollsBackClosesConnectionAndCallsRollbackHooks()
    {
        var id = NewId();
        _queue.Register("a", (_, _, _) => Task.FromResult(true));

        await _sut.StartTransactionAsync(CancellationToken.None);
        await InsertAsync(id);
        await _queue.RunPendingAsync(_sut.Connection, _sut.Transaction, CancellationToken.None);
        await _sut.DisposeAsync();

        Assert.Null(_sut.Transaction);
        Assert.Equal(ConnectionState.Closed, _connection.State);
        Assert.True(_queue.HasPending);
        Assert.Equal(0, await CountAsync(id));
    }

    [Fact]
    public async Task CommitAndRollback_WithoutTransaction_DoNothing()
    {
        await _sut.CommitAsync(CancellationToken.None);
        await _sut.RollbackAsync(CancellationToken.None);

        Assert.Null(_sut.Transaction);
    }

    private static string NewId() => Guid.NewGuid().ToString();

    private async Task InsertAsync(string id)
    {
        await _sut.Connection.ExecuteAsync("INSERT INTO uow_items (id) VALUES (@Id)", new { Id = id },
            _sut.Transaction);
    }

    private async Task<int> CountAsync(string id)
    {
        await using var connection = await db.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM uow_items WHERE id = @Id",
            new { Id = id });
    }
}
