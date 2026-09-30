using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Stock.Application.Abstractions;
using Stock.Application.Commands.ChangeStockName;
using Stock.Application.Events;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class ChangeStockNameHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private IStockWriter Writer { get; init; }
    private ChangeStockNameHandler Handler { get; init; }
    private MssqlFixture MssqlFixture { get; init; }
    private TestDbContext DbContext { get; init; }
    private IUnitOfWork UnitOfWork { get; init; }

    public ChangeStockNameHandlerTests(MssqlFixture mssqlFixture)
    {
        MssqlFixture = mssqlFixture;
        var dbContext = new TestDbContext(MssqlFixture.ConnectionString);
        DbContext = dbContext;
        var unitOfWork = new UnitOfWork(new TestDbConnectionFactory(MssqlFixture.ConnectionString));
        UnitOfWork = unitOfWork;
        Writer = new StockDataWriter(unitOfWork);
        var decorator = new UnitOfWorkDecorator(unitOfWork, new ResiliencePipelineBuilder().Build(),
            NullLogger<UnitOfWorkDecorator>.Instance);
        Handler = new ChangeStockNameHandler(Writer, new OutboxWriter(unitOfWork), decorator);
    }

    public async Task InitializeAsync()
    {
        await DbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(DbContext);
    }

    public async Task DisposeAsync()
    {
        await UnitOfWork.DisposeAsync();
        await DbContext.Connection.CloseAsync();
    }

    [Fact]
    public async Task Handle_ShouldChangeStockName()
    {
        var stockId = Guid.NewGuid();
        await Seed(stockId);
        var command = new ChangeStockNameCommand(stockId, "NewName");

        await Handler.Handle(command, CancellationToken.None);

        var updatedStock = await DbContext.Connection.QuerySingleAsync(
            "SELECT * FROM stock_data WHERE id = @Id",
            new { Id = stockId }
        );

        Assert.Equal("NewName", updatedStock.name);
    }

    [Fact]
    public async Task Handle_ShouldWriteNameChangedEventWithIncrementedVersion()
    {
        var stockId = Guid.NewGuid();
        await Seed(stockId);

        await Handler.Handle(new ChangeStockNameCommand(stockId, "First"), CancellationToken.None);
        await Handler.Handle(new ChangeStockNameCommand(stockId, "Second"), CancellationToken.None);

        var events = (await DbContext.Connection.QueryAsync<string>(
                "SELECT payload FROM outbox WHERE aggregate_id = @Id", new { Id = stockId }))
            .Select(p => JsonSerializer.Deserialize<NameChangedEvent>(p)!)
            .OrderBy(e => e.Version)
            .ToList();

        Assert.Equal([1L, 2L], events.Select(e => e.Version));
        Assert.Equal(["First", "Second"], events.Select(e => e.Name));
    }

    [Fact]
    public async Task Handle_ShouldReturnFalseAndWriteNoEvent_WhenStockDoesNotExist()
    {
        var stockId = Guid.NewGuid();

        var found = await Handler.Handle(new ChangeStockNameCommand(stockId, "NewName"), CancellationToken.None);

        Assert.False(found);
        Assert.Equal(0, await DbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM outbox WHERE aggregate_id = @Id", new { Id = stockId }));
    }

    [Fact]
    public async Task Handle_ShouldKeepOldNameAndVersion_WhenOutboxWriteFails()
    {
        var stockId = Guid.NewGuid();
        await Seed(stockId);

        await TestDatabase.WithTableOfflineAsync(DbContext, "outbox", async () =>
            await Assert.ThrowsAsync<SqlException>(() =>
                Handler.Handle(new ChangeStockNameCommand(stockId, "NewName"), CancellationToken.None)));

        var stock = await DbContext.Connection.QuerySingleAsync(
            "SELECT name, metadata_version FROM stock_data WHERE id = @Id", new { Id = stockId });
        Assert.Equal("OldName", stock.name);
        Assert.Equal(0L, (long)stock.metadata_version);
    }

    private async Task Seed(Guid id)
    {
        await DbContext.Connection.ExecuteAsync(
            "INSERT INTO stock_data (id, name, is_open_to_trade, trading_start_time, trading_end_time, currency) VALUES (@Id, @Name, 1, @OpenTime, @CloseTime, @Currency)",
            new
            {
                Id = id,
                Name = "OldName",
                OpenTime = TimeSpan.FromHours(9),
                CloseTime = TimeSpan.FromHours(17),
                Currency = "USD"
            });
    }
}