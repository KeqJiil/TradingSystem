using System.Data.Common;
using Messaging.Dapper.DbTasks;
using Microsoft.Data.SqlClient;

namespace Messaging.Dapper.Tests.UnitTests;

public class DbTaskQueueTests : IDisposable
{
    private readonly SqlConnection _connection = new();
    private readonly DbTaskQueue _queue = new();

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task RunPending_RegisteredTask_RunsWithGivenConnection()
    {
        (DbConnection Connection, DbTransaction? Transaction)? received = null;
        _queue.Register("a", (connection, transaction, _) =>
        {
            received = (connection, transaction);
            return Task.FromResult(true);
        });

        var result = await _queue.RunPendingAsync(_connection, null, CancellationToken.None);

        Assert.True(result);
        Assert.NotNull(received);
        Assert.Same(_connection, received.Value.Connection);
        Assert.Null(received.Value.Transaction);
    }

    [Fact]
    public async Task RunPending_SeveralTasks_RunInRegistrationOrder()
    {
        var calls = new List<string>();
        foreach (var key in new[] { "a", "b", "c" })
            _queue.Register(key, Record(calls, key));

        await _queue.RunPendingAsync(_connection, null, CancellationToken.None);

        Assert.Equal(["a", "b", "c"], calls);
    }

    [Fact]
    public async Task RunPending_TaskReturnsFalse_ReturnsFalseSetsVetoedAndSkipsRest()
    {
        var calls = new List<string>();
        _queue.Register("a", Record(calls, "a"));
        _queue.Register("b", Record(calls, "b", false));
        _queue.Register("c", Record(calls, "c"));

        var result = await _queue.RunPendingAsync(_connection, null, CancellationToken.None);

        Assert.False(result);
        Assert.True(_queue.Vetoed);
        Assert.Equal(["a", "b"], calls);
    }

    [Fact]
    public async Task Register_SameKeyTwice_KeepsFirstTaskAndRunsItOnce()
    {
        var calls = new List<string>();
        _queue.Register("a", Record(calls, "first"));
        _queue.Register("a", Record(calls, "second"));

        await _queue.RunPendingAsync(_connection, null, CancellationToken.None);

        Assert.Equal(["first"], calls);
    }

    [Fact]
    public async Task HasPending_TrueAfterRegister_FalseAfterRun()
    {
        Assert.False(_queue.HasPending);

        _queue.Register("a", (_, _, _) => Task.FromResult(true));
        Assert.True(_queue.HasPending);

        await _queue.RunPendingAsync(_connection, null, CancellationToken.None);
        Assert.False(_queue.HasPending);
        Assert.False(_queue.Vetoed);
    }

    [Fact]
    public async Task RunPending_WithoutTransaction_DoesNotRunTaskAgain()
    {
        var calls = new List<string>();
        _queue.Register("a", Record(calls, "a"));

        await _queue.RunPendingAsync(_connection, null, CancellationToken.None);
        _queue.OnRolledBack();
        await _queue.RunPendingAsync(_connection, null, CancellationToken.None);

        Assert.Equal(["a"], calls);
    }

    [Fact]
    public async Task RunPending_TaskRegisteredAfterRun_RunsOnlyNewTask()
    {
        var calls = new List<string>();
        _queue.Register("a", Record(calls, "a"));
        await _queue.RunPendingAsync(_connection, null, CancellationToken.None);

        _queue.Register("b", Record(calls, "b"));
        await _queue.RunPendingAsync(_connection, null, CancellationToken.None);

        Assert.Equal(["a", "b"], calls);
    }

    private static DbTask Record(List<string> calls, string name, bool result = true)
    {
        return (_, _, _) =>
        {
            calls.Add(name);
            return Task.FromResult(result);
        };
    }
}
