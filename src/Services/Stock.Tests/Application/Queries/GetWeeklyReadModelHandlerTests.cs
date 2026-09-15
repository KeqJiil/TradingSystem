using Dapper;
using Stock.Application.Abstractions;
using Stock.Application.Queries.GetWeeklyReadModel;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Queries;

public class GetWeeklyReadModelHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private IStockPriceHistoryReader Reader { get; init; }
    private GetWeeklyReadModelHandler Handler { get; init; }
    private MssqlFixture MssqlFixture { get; init; }
    private TestDbContext DbContext { get; init; }

    public GetWeeklyReadModelHandlerTests(MssqlFixture mssqlFixture)
    {
        MssqlFixture = mssqlFixture;
        var dbContext = new TestDbContext(MssqlFixture.ConnectionString);
        DbContext = dbContext;
        Reader = new StockPriceHistoryReader(dbContext);
        Handler = new GetWeeklyReadModelHandler(Reader);
    }

    public async Task InitializeAsync()
    {
        await DbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await StockTestSchema.EnsureDailyStockDataProjectionCreatedAsync(DbContext);
    }

    public async Task DisposeAsync()
    {
        await DbContext.Connection.ExecuteAsync("DROP TABLE IF EXISTS daily_stock_data_projection");
        await DbContext.Connection.CloseAsync();
    }

    [Fact]
    public async Task Handle_ShouldWrapDailyProjections_WithinRangeInWeeklyReadModel()
    {
        var stockId = Guid.NewGuid();
        var startDate = new DateOnly(2026, 3, 1);
        var endDate = new DateOnly(2026, 3, 3);
        await Seed(stockId, new DateOnly(2026, 2, 28)); // before range
        await Seed(stockId, new DateOnly(2026, 3, 1)); // in range
        await Seed(stockId, new DateOnly(2026, 3, 2)); // in range
        await Seed(stockId, new DateOnly(2026, 3, 3)); // outside range (exclusive upper bound)
        await Seed(Guid.NewGuid(), new DateOnly(2026, 3, 1)); // different aggregate

        var result = await Handler.Handle(
            new GetWeeklyReadModelQuery(stockId, startDate, endDate), CancellationToken.None);

        Assert.Equal(stockId, result.AggregateId);
        Assert.Equal(2, result.DailyReadModels.Count);
        Assert.All(result.DailyReadModels, m => Assert.True(m.Date >= startDate && m.Date < endDate));
    }

    private async Task Seed(Guid aggregateId, DateOnly date)
    {
        await DbContext.Connection.ExecuteAsync("""
            INSERT INTO daily_stock_data_projection
                (id, aggregate_id, open_price, low_price, high_price, close_price, price_difference, last_version, date)
            VALUES
                (@Id, @AggregateId, 10, 8, 15, 12, 2, 1, @Date)
            """,
            new { Id = Guid.NewGuid(), AggregateId = aggregateId, Date = date });
    }
}
