using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Stock.Application.Abstractions;
using Stock.Application.Commands.CreateStock;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class CreateStockHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private IStockWriter Writer { get; init; }
    private IOutboxWriter OutboxWriter { get; init; }
    private IUnitOfWork UnitOfWork { get; init; }
    private IUnitOfWorkDecorator UnitOfWorkDecorator { get; init; }
    private ResiliencePipeline ResiliencePipeline { get; init; }
    private CreateStockHandler Handler { get; init; }
    private MssqlFixture MssqlFixture { get; init; }
    private TestDbContext DbContext { get; init; }

    public CreateStockHandlerTests(MssqlFixture mssqlFixture)
    {
        MssqlFixture = mssqlFixture;
        var dbContext = new TestDbContext(MssqlFixture.ConnectionString);
        DbContext = dbContext;
        Writer = new StockDataWriter(dbContext);
        OutboxWriter = new OutboxWriter(dbContext);
        ResiliencePipeline = new ResiliencePipelineBuilder().Build();
        UnitOfWork = new UnitOfWork(new TestDbConnectionFactory(MssqlFixture.ConnectionString));
        UnitOfWorkDecorator = new UnitOfWorkDecorator(UnitOfWork, ResiliencePipeline,
            NullLogger<UnitOfWorkDecorator>.Instance);
        Handler = new CreateStockHandler(Writer, new StockDataReader(dbContext), OutboxWriter, UnitOfWorkDecorator);
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
    public async Task Handle_ShouldCreateStockAndWriteOutboxEvent()
    {
        var command = NewCommand(Guid.NewGuid());

        var result = await Handler.Handle(command, CancellationToken.None);

        Assert.Equal(CreateStockResult.Created, result);

        var stock = await DbContext.Connection.QuerySingleAsync(
            "SELECT * FROM stock_data WHERE id = @Id",
            new { command.Id }
        );
        Assert.Equal(command.Name, stock.name);
        Assert.Equal(command.Currency, stock.currency);
        Assert.Equal(1, await OutboxCount(command.Id));
    }

    [Fact]
    public async Task Handle_RepeatedWithSameBody_ShouldReturnAlreadyExistsWithoutSecondEvent()
    {
        var command = NewCommand(Guid.NewGuid());

        await Handler.Handle(command, CancellationToken.None);
        var result = await Handler.Handle(command, CancellationToken.None);

        Assert.Equal(CreateStockResult.AlreadyExists, result);
        Assert.Equal(1, await OutboxCount(command.Id));
    }

    [Fact]
    public async Task Handle_RepeatedWithDifferentBody_ShouldReturnConflictAndKeepOriginal()
    {
        var command = NewCommand(Guid.NewGuid());

        await Handler.Handle(command, CancellationToken.None);
        var result = await Handler.Handle(command with { Currency = "EUR" }, CancellationToken.None);

        Assert.Equal(CreateStockResult.Conflict, result);
        Assert.Equal("USD", await DbContext.Connection.ExecuteScalarAsync<string>(
            "SELECT currency FROM stock_data WHERE id = @Id", new { command.Id }));
        Assert.Equal(1, await OutboxCount(command.Id));
    }

    private static CreateStockCommand NewCommand(Guid id)
    {
        return new CreateStockCommand(id, "StockName", true, "USD", new TimeOnly(9, 0), new TimeOnly(17, 0));
    }

    private Task<int> OutboxCount(Guid id)
    {
        return DbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM outbox WHERE aggregate_id = @Id", new { Id = id });
    }
}
