using Dapper;
using Stock.Application.Abstractions;
using Stock.Application.Commands.ChangeStockName;
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

    public ChangeStockNameHandlerTests(MssqlFixture mssqlFixture)
    {
        MssqlFixture = mssqlFixture;
        var dbContext = new TestDbContext(MssqlFixture.ConnectionString);
        DbContext = dbContext;
        Writer = new StockDataWriter(dbContext);
        Handler = new ChangeStockNameHandler(Writer);
    }

    public async Task InitializeAsync()
    {
        await DbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await DbContext.Connection.ExecuteAsync("""
                                                IF OBJECT_ID('stock_data') IS NULL
                                                CREATE TABLE stock_data (
                                                    id UNIQUEIDENTIFIER PRIMARY KEY,
                                                    name NVARCHAR(100) NOT NULL,
                                                    is_open_to_trade BIT NOT NULL,
                                                    trading_start_time TIME NOT NULL,
                                                    trading_end_time TIME NOT NULL,
                                                    currency NVARCHAR(10) NOT NULL
                                                )
                                                """
        );
    }

    public async Task DisposeAsync()
    {
        await DbContext.Connection.ExecuteAsync("DROP TABLE IF EXISTS stock_data");
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