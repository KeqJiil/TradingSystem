using Dapper;
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
        UnitOfWorkDecorator = new UnitOfWorkDecorator(UnitOfWork, ResiliencePipeline);
        Handler = new CreateStockHandler(Writer, OutboxWriter, UnitOfWorkDecorator);
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
        var command = new CreateStockCommand("StockName", true, "USD", new TimeOnly(9, 0), new TimeOnly(17, 0));

        var id = await Handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);

        var stock = await DbContext.Connection.QuerySingleAsync(
            "SELECT * FROM stock_data WHERE id = @Id",
            new { Id = id }
        );
        Assert.Equal(command.Name, stock.name);
        Assert.Equal(command.Currency, stock.currency);

        var outboxCount = await DbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM outbox WHERE aggregate_id = @Id",
            new { Id = id }
        );
        Assert.Equal(1, outboxCount);
    }

    [Fact]
    public async Task Handle_ShouldReturnNewGuidEachTime()
    {
        var command = new CreateStockCommand("StockName", true, "USD", new TimeOnly(9, 0), new TimeOnly(17, 0));

        var firstId = await Handler.Handle(command, CancellationToken.None);
        var secondId = await Handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, firstId);
        Assert.NotEqual(Guid.Empty, secondId);
        Assert.NotEqual(firstId, secondId);
    }
}