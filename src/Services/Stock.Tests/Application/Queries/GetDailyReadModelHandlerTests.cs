using Dapper;
using Stock.Application.Abstractions;
using Stock.Application.Queries.GetDailyReadModel;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Queries;

public class GetDailyReadModelHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private IStockPriceHistoryReader Reader { get; init; }
    private GetDailyReadModelHandler Handler { get; init; }
    private MssqlFixture MssqlFixture { get; init; }
    private TestDbContext DbContext { get; init; }

    public GetDailyReadModelHandlerTests(MssqlFixture mssqlFixture)
    {
        MssqlFixture = mssqlFixture;
        var dbContext = new TestDbContext(MssqlFixture.ConnectionString);
        DbContext = dbContext;
        Reader = new StockPriceHistoryReader(dbContext);
        Handler = new GetDailyReadModelHandler(Reader);
    }

    public async Task InitializeAsync()
    {
        await DbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(DbContext);
    }

    public async Task DisposeAsync()
    {
        await DbContext.Connection.CloseAsync();
    }

    [Fact]
    public async Task Handle_ShouldMapEachFieldFromProjectionRow_IntoCorrectPosition()
    {
        var stockId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 5);
        await Seed(stockId, date, openPrice: 10m, lowPrice: 8m, highPrice: 15m, closePrice: 12m, difference: 2m);

        var result = await Handler.Handle(new GetDailyReadModelQuery(stockId, date), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(stockId, result.Value.AggregateId);
        Assert.Equal(date, result.Value.Date);
        Assert.Equal(10m, result.Value.OpenPrice);
        Assert.Equal(12m, result.Value.ClosePrice);
        Assert.Equal(15m, result.Value.HighPrice);
        Assert.Equal(8m, result.Value.LowPrice);
        Assert.Equal(2m, result.Value.PriceDifference);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenNoProjectionForDay()
    {
        var result = await Handler.Handle(
            new GetDailyReadModelQuery(Guid.NewGuid(), new DateOnly(2026, 3, 5)), CancellationToken.None);

        Assert.Null(result);
    }

    private async Task Seed(Guid aggregateId, DateOnly date, decimal openPrice, decimal lowPrice, decimal highPrice,
        decimal closePrice, decimal difference)
    {
        await DbContext.Connection.ExecuteAsync("""
            INSERT INTO daily_stock_data_projection
                (id, aggregate_id, open_price, low_price, high_price, close_price, price_difference, last_version, date)
            VALUES
                (@Id, @AggregateId, @OpenPrice, @LowPrice, @HighPrice, @ClosePrice, @Difference, @LastVersion, @Date)
            """,
            new
            {
                Id = Guid.NewGuid(),
                AggregateId = aggregateId,
                OpenPrice = openPrice,
                LowPrice = lowPrice,
                HighPrice = highPrice,
                ClosePrice = closePrice,
                Difference = difference,
                LastVersion = 1L,
                Date = date
            });
    }
}
