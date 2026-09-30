using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Stock.Infrastructure.Persistence.Implementations;
using Xunit;

namespace Stock.Tests.Infrastructure.Persistence;

public class UnitOfWorkDecoratorTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly UnitOfWork _unitOfWork;
    private readonly UnitOfWorkDecorator _decorator;
    private TestDbContext DbContext { get; }

    public UnitOfWorkDecoratorTests(MssqlFixture fixture)
    {
        DbContext = new TestDbContext(fixture.ConnectionString);
        _unitOfWork = new UnitOfWork(new TestDbConnectionFactory(fixture.ConnectionString));
        _decorator = new UnitOfWorkDecorator(_unitOfWork, new ResiliencePipelineBuilder().Build(),
            NullLogger<UnitOfWorkDecorator>.Instance);
    }

    public async Task InitializeAsync()
    {
        await DbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(DbContext);
    }

    public async Task DisposeAsync()
    {
        await _unitOfWork.DisposeAsync();
        await DbContext.DisposeAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCommit_WhenActionSucceeds()
    {
        var id = Guid.NewGuid();

        await _decorator.ExecuteAsync(() => InsertStock(id), CancellationToken.None);

        Assert.Null(_unitOfWork.Transaction);
        Assert.Equal(1, await CountStocks(id));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRollbackAndRethrow_WhenActionThrows()
    {
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _decorator.ExecuteAsync(async () =>
        {
            await InsertStock(id);
            throw new InvalidOperationException("action failed");
        }, CancellationToken.None));

        Assert.Null(_unitOfWork.Transaction);
        Assert.Equal(0, await CountStocks(id));
    }

    [Fact]
    public async Task ExecuteAsync_Nested_InnerDoesNotCommit_OnlyOuterDoes()
    {
        var outerId = Guid.NewGuid();
        var innerId = Guid.NewGuid();
        var visibleAfterInner = -1;

        await _decorator.ExecuteAsync(async () =>
        {
            await InsertStock(outerId);
            await _decorator.ExecuteAsync(() => InsertStock(innerId), CancellationToken.None);

            Assert.NotNull(_unitOfWork.Transaction);
            visibleAfterInner = await CountCommittedStocks(outerId, innerId);
        }, CancellationToken.None);

        Assert.Equal(0, visibleAfterInner);
        Assert.Equal(1, await CountStocks(outerId));
        Assert.Equal(1, await CountStocks(innerId));
    }

    [Fact]
    public async Task ExecuteAsync_Nested_InnerThrows_RollsBackOuterWorkToo()
    {
        var outerId = Guid.NewGuid();
        var innerId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _decorator.ExecuteAsync(async () =>
        {
            await InsertStock(outerId);
            await _decorator.ExecuteAsync(async () =>
            {
                await InsertStock(innerId);
                throw new InvalidOperationException("inner failed");
            }, CancellationToken.None);
        }, CancellationToken.None));

        Assert.Equal(0, await CountStocks(outerId));
        Assert.Equal(0, await CountStocks(innerId));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRethrowOriginalException_WhenRollbackFails()
    {
        var id = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _decorator.ExecuteAsync(async () =>
        {
            await InsertStock(id);
            await _unitOfWork.Connection.CloseAsync();
            throw new InvalidOperationException("original failure");
        }, CancellationToken.None));

        Assert.Equal("original failure", exception.Message);
        Assert.Null(_unitOfWork.Transaction);
        Assert.Equal(0, await CountStocks(id));
    }

    private Task InsertStock(Guid id)
    {
        return _unitOfWork.Connection.ExecuteAsync(
            "INSERT INTO stock_data (id, name, is_open_to_trade, trading_start_time, trading_end_time, currency) VALUES (@Id, @Name, 1, @OpenTime, @CloseTime, @Currency)",
            new
            {
                Id = id,
                Name = "UowStock",
                OpenTime = TimeSpan.FromHours(9),
                CloseTime = TimeSpan.FromHours(17),
                Currency = "USD"
            }, _unitOfWork.Transaction);
    }

    private Task<int> CountStocks(Guid id)
    {
        return DbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM stock_data WHERE id = @Id", new { Id = id });
    }

    private Task<int> CountCommittedStocks(params Guid[] ids)
    {
        return DbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM stock_data WITH (READPAST) WHERE id IN @Ids", new { Ids = ids });
    }
}
