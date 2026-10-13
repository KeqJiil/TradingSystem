using System.Data.Common;
using Messaging.Dapper.DbTasks;
using Messaging.Dapper.Tests.Integration.Infrastructure;
using Microsoft.Data.SqlClient;

namespace Messaging.Dapper.Tests.Integration.Transactions;

public class DbTaskQueueTransactionTests(MssqlFixture db) : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly DbTaskQueue _queue = new();
    private SqlConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = await db.OpenConnectionAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task OnCommitting_RegisteredTask_RunsWithGivenTransaction()
    {
        await using var transaction = await _connection.BeginTransactionAsync();
        DbTransaction? received = null;
        _queue.Register("a", (_, tx, _) =>
        {
            received = tx;
            return Task.FromResult(true);
        });

        var result = await _queue.OnCommittingAsync(_connection, transaction, CancellationToken.None);

        Assert.True(result);
        Assert.Same(transaction, received);
    }

    [Fact]
    public async Task OnCommitting_TaskReturnsFalse_ReturnsFalseAndSetsVetoed()
    {
        await using var transaction = await _connection.BeginTransactionAsync();
        _queue.Register("a", (_, _, _) => Task.FromResult(false));

        var result = await _queue.OnCommittingAsync(_connection, transaction, CancellationToken.None);

        Assert.False(result);
        Assert.True(_queue.Vetoed);
    }

    [Fact]
    public async Task OnRolledBack_AfterTasksRanInTransaction_TasksRunAgainOnNextCommit()
    {
        var calls = 0;
        _queue.Register("a", (_, _, _) =>
        {
            calls++;
            return Task.FromResult(true);
        });

        await using (var first = await _connection.BeginTransactionAsync())
        {
            await _queue.OnCommittingAsync(_connection, first, CancellationToken.None);
            await first.RollbackAsync();
            _queue.OnRolledBack();
        }

        Assert.True(_queue.HasPending);

        await using var second = await _connection.BeginTransactionAsync();
        await _queue.OnCommittingAsync(_connection, second, CancellationToken.None);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task OnCommitted_AfterTasksRanInTransaction_TasksDoNotRunAgain()
    {
        var calls = 0;
        _queue.Register("a", (_, _, _) =>
        {
            calls++;
            return Task.FromResult(true);
        });

        await using (var first = await _connection.BeginTransactionAsync())
        {
            await _queue.OnCommittingAsync(_connection, first, CancellationToken.None);
            await first.CommitAsync();
            _queue.OnCommitted();
        }

        _queue.OnRolledBack();

        await using var second = await _connection.BeginTransactionAsync();
        await _queue.OnCommittingAsync(_connection, second, CancellationToken.None);

        Assert.Equal(1, calls);
        Assert.False(_queue.HasPending);
    }

    [Fact]
    public async Task OnRolledBack_TaskNotYetRun_StaysPendingAndRunsOnce()
    {
        var calls = 0;
        _queue.Register("a", (_, _, _) =>
        {
            calls++;
            return Task.FromResult(true);
        });

        _queue.OnRolledBack();

        await using var transaction = await _connection.BeginTransactionAsync();
        await _queue.OnCommittingAsync(_connection, transaction, CancellationToken.None);

        Assert.Equal(1, calls);
    }
}
