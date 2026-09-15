using Dapper;
using Stock.Application.Abstractions;
using Stock.Application.Queries.GetReadModel;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Queries;

public class GetReadModelHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private IStockReader Reader { get; init; }
    private GetReadModelHandler Handler { get; init; }
    private MssqlFixture MssqlFixture { get; init; }
    private TestDbContext DbContext { get; init; }

    public GetReadModelHandlerTests(MssqlFixture mssqlFixture)
    {
        MssqlFixture = mssqlFixture;
        var dbContext = new TestDbContext(MssqlFixture.ConnectionString);
        DbContext = dbContext;
        Reader = new StockReader(dbContext);
        Handler = new GetReadModelHandler(Reader);
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
    public async Task Handle_ShouldReturnReadModel_ForExistingStock()
    {
        var stockId = Guid.NewGuid();
        await Seed(stockId, "StockName", true, "USD");

        var result = await Handler.Handle(new GetReadModelQuery(stockId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(stockId, result.Value.AggregateId);
        Assert.Equal("StockName", result.Value.Name);
        Assert.True(result.Value.IsOpenToTrade);
        Assert.Equal("USD", result.Value.Currency);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenStockDoesNotExist()
    {
        var result = await Handler.Handle(new GetReadModelQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    private async Task Seed(Guid aggregateId, string name, bool isOpenToTrade, string currency)
    {
        await DbContext.Connection.ExecuteAsync("""
            INSERT INTO stock_data_projection
                (aggregate_id, name, is_open_to_trade, price, version, trading_start_time, trading_end_time, currency)
            VALUES
                (@AggregateId, @Name, @IsOpenToTrade, @Price, @Version, @OpenTime, @CloseTime, @Currency)
            """,
            new
            {
                AggregateId = aggregateId,
                Name = name,
                IsOpenToTrade = isOpenToTrade,
                Price = 0m,
                Version = 0L,
                OpenTime = TimeSpan.FromHours(9),
                CloseTime = TimeSpan.FromHours(17),
                Currency = currency
            });
    }
}
