using Dapper;
using Messaging.Dapper.Tests.Integration.Infrastructure;
using Messaging.Dapper.Transactions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Messaging.Dapper.Tests.Integration.Transactions;

public class UnitOfWorkDecoratorTests(MssqlFixture db) : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly CapturingLogger<UnitOfWorkDecorator> _logger = new();
    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;

    private IUnitOfWorkDecorator Decorator => _scope.ServiceProvider.GetRequiredService<IUnitOfWorkDecorator>();
    private ITransactionContext Context => _scope.ServiceProvider.GetRequiredService<ITransactionContext>();

    public async Task InitializeAsync()
    {
        await using var connection = await db.OpenConnectionAsync();
        await connection.ExecuteAsync("""
                                      IF OBJECT_ID('dbo.uow_items', 'U') IS NULL
                                          CREATE TABLE uow_items (id VARCHAR(100) NOT NULL);
                                      """);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUnitOfWork(_ => new SqlConnection(db.ConnectionString));
        services.AddSingleton<ILogger<UnitOfWorkDecorator>>(_logger);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateAsyncScope();
    }

    public async Task DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Execute_ActionSucceeds_CommitsData()
    {
        var id = NewId();

        await Decorator.ExecuteAsync(() => InsertAsync(id), CancellationToken.None);

        Assert.Null(Context.Transaction);
        Assert.Equal(1, await CountAsync(id));
    }

    [Fact]
    public async Task Execute_Nested_InnerRunsInOuterTransaction()
    {
        var id = NewId();
        var transactions = new List<object?>();

        await Decorator.ExecuteAsync(async () =>
        {
            transactions.Add(Context.Transaction);
            await Decorator.ExecuteAsync(async () =>
            {
                transactions.Add(Context.Transaction);
                await InsertAsync(id);
            }, CancellationToken.None);
            transactions.Add(Context.Transaction);
        }, CancellationToken.None);

        Assert.NotNull(transactions[0]);
        Assert.All(transactions, t => Assert.Same(transactions[0], t));
        Assert.Equal(1, await CountAsync(id));
    }

    [Fact]
    public async Task Execute_OuterFailsAfterNestedSucceeded_RollsBackInnerWork()
    {
        var id = NewId();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Decorator.ExecuteAsync(async () =>
        {
            await Decorator.ExecuteAsync(() => InsertAsync(id), CancellationToken.None);
            throw new InvalidOperationException("outer failed");
        }, CancellationToken.None));

        Assert.Equal(0, await CountAsync(id));
    }

    [Fact]
    public async Task Execute_TransientFailure_RollsBackAttemptAndRetries()
    {
        var id = NewId();
        var attempts = 0;

        await Decorator.ExecuteAsync(async () =>
        {
            attempts++;
            await InsertAsync(id);
            if (attempts == 1)
                await Context.Connection.ExecuteAsync("WAITFOR DELAY '00:00:05'", transaction: Context.Transaction,
                    commandTimeout: 1);
        }, CancellationToken.None);

        Assert.Equal(2, attempts);
        Assert.Equal(1, await CountAsync(id));
    }

    [Fact]
    public async Task Execute_TransactionStartedOutside_TransientFailureIsNotRetried()
    {
        var attempts = 0;
        await _scope.ServiceProvider.GetRequiredService<IUnitOfWork>().StartTransactionAsync(CancellationToken.None);

        var ex = await Assert.ThrowsAsync<SqlException>(() => Decorator.ExecuteAsync(async () =>
        {
            attempts++;
            await Context.Connection.ExecuteAsync("WAITFOR DELAY '00:00:05'", transaction: Context.Transaction,
                commandTimeout: 1);
        }, CancellationToken.None));

        Assert.Equal(-2, ex.Number);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Execute_NonTransientFailure_RollsBackAndRethrowsWithoutRetry()
    {
        var id = NewId();
        var attempts = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Decorator.ExecuteAsync(async () =>
        {
            attempts++;
            await InsertAsync(id);
            throw new InvalidOperationException("not transient");
        }, CancellationToken.None));

        Assert.Equal(1, attempts);
        Assert.Null(Context.Transaction);
        Assert.Equal(0, await CountAsync(id));
    }

    [Fact]
    public async Task Execute_RollbackFails_RethrowsOriginalExceptionAndLogsRollbackError()
    {
        var ex = await Assert.ThrowsAsync<ApplicationException>(() => Decorator.ExecuteAsync(async () =>
        {
            await InsertAsync(NewId());
            await Context.Connection.CloseAsync();
            throw new ApplicationException("original");
        }, CancellationToken.None));

        Assert.Equal("original", ex.Message);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("Rollback failed"));
    }

    private static string NewId() => Guid.NewGuid().ToString();

    private async Task InsertAsync(string id)
    {
        await Context.EnsureConnectionOpenAsync(CancellationToken.None);
        await Context.Connection.ExecuteAsync("INSERT INTO uow_items (id) VALUES (@Id)", new { Id = id },
            Context.Transaction);
    }

    private async Task<int> CountAsync(string id)
    {
        await using var connection = await db.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM uow_items WHERE id = @Id",
            new { Id = id });
    }
}
