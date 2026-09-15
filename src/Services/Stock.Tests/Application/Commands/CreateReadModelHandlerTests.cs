using Dapper;
using Stock.Application.Abstractions;
using Stock.Application.Commands.CreateReadModel;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class CreateReadModelHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private IStockReadModelWriter Writer { get; init; }
    private CreateReadModelHandler Handler { get; init; }
    private MssqlFixture MssqlFixture { get; init; }
    private TestDbContext DbContext { get; init; }

    public CreateReadModelHandlerTests(MssqlFixture mssqlFixture)
    {
        MssqlFixture = mssqlFixture;
        var dbContext = new TestDbContext(MssqlFixture.ConnectionString);
        DbContext = dbContext;
        Writer = new StockReadModelWriter(dbContext);
        Handler = new CreateReadModelHandler(Writer);
    }

    public async Task InitializeAsync()
    {
        await DbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await StockTestSchema.EnsureStockDataProjectionCreatedAsync(DbContext);
    }

    public async Task DisposeAsync()
    {
        await DbContext.Connection.ExecuteAsync("DROP TABLE IF EXISTS stock_data_projection");
        await DbContext.Connection.CloseAsync();
    }

    [Fact]
    public async Task Handle_ShouldCreateReadModel()
    {
        var command = new CreateReadModelCommand(Guid.NewGuid(), "StockName", true, "USD",
            TimeOnly.FromDateTime(DateTime.Now), TimeOnly.FromDateTime(DateTime.Now.AddHours(1)));

        await Handler.Handle(command, CancellationToken.None);

        var createdStock = await DbContext.Connection.QuerySingleAsync<StockReadModel>("""
            SELECT
                aggregate_id AS AggregateId,
                name AS Name,
                price AS Price,
                currency AS Currency,
                is_open_to_trade AS IsOpenToTrade,
                trading_start_time AS TradingStartTime,
                trading_end_time AS TradingEndTime,
                updated_at AS UpdatedAt
            FROM stock_data_projection WHERE aggregate_id = @Id
            """,
            new { Id = command.AggregateId }
        );

        Assert.Equal(command.Name, createdStock.Name);
        Assert.Equal(command.IsOpenToTrade, createdStock.IsOpenToTrade);
        Assert.Equal(command.TradingStartTime, createdStock.TradingStartTime);
        Assert.Equal(command.TradingCloseTime, createdStock.TradingEndTime);
        Assert.Equal(command.Currency, createdStock.Currency);
    }

    [Fact]
    public async Task Handle_ShouldNotCreateDuplicateReadModel()
    {
        var stockId = Guid.NewGuid();
        var command = new CreateReadModelCommand(stockId, "StockName", true, "USD", TimeOnly.FromDateTime(DateTime.Now),
            TimeOnly.FromDateTime(DateTime.Now.AddHours(1)));
        await Handler.Handle(command, CancellationToken.None);

        var readModels = await DbContext.Connection.QueryAsync(
            "SELECT * FROM stock_data_projection WHERE aggregate_id = @Id",
            new { Id = stockId }
        );

        Assert.Single(readModels);
    }
}