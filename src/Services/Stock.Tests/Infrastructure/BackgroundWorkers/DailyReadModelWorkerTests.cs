using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Stock.Application.Events;
using Stock.Infrastructure.BackgroundWorkers;
using Stock.Infrastructure.Persistence.Implementations;
using Xunit;

namespace Stock.Tests.Infrastructure.BackgroundWorkers;

public class DailyReadModelWorkerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private static readonly DateOnly Date = new(2026, 3, 5);
    private static readonly DateTimeOffset DayStart = new(2026, 3, 5, 0, 0, 0, TimeSpan.Zero);

    private const string InsertEventSql = """
                                          INSERT INTO events_store (event_id, aggregate_id, version, event_type, payload, price_change, occured_at)
                                          VALUES (@EventId, @AggregateId, @Version, @EventType, @Payload, @PriceChange, @OccuredAt)
                                          """;

    private TestDbContext DbContext { get; init; }
    private DailyReadModelWorker Worker { get; init; }

    public DailyReadModelWorkerTests(MssqlFixture mssqlFixture)
    {
        var dbContext = new TestDbContext(mssqlFixture.ConnectionString);
        DbContext = dbContext;
        Worker = new DailyReadModelWorker(
            NullLogger<DailyReadModelWorker>.Instance,
            new StockEventStoreReader(dbContext),
            new StockPriceHistoryReader(dbContext),
            new StockDailyReadModelWriter(dbContext));
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
    public async Task ProcessAsync_ShouldComputeOhlcFromCumulativePrice()
    {
        var aggregateId = Guid.NewGuid();
        await SeedEvent(aggregateId, 1, 10m, DayStart.AddHours(10));
        await SeedEvent(aggregateId, 2, 5m, DayStart.AddHours(11));
        await SeedEvent(aggregateId, 3, -3m, DayStart.AddHours(12));

        await Worker.ProcessAsync(new DailyReadModelRequested(aggregateId, Date), CancellationToken.None);

        var row = await ReadDaily(aggregateId);
        Assert.Equal(10m, row.OpenPrice);
        Assert.Equal(15m, row.HighPrice);
        Assert.Equal(10m, row.LowPrice);
        Assert.Equal(12m, row.ClosePrice);
        Assert.Equal(2m, row.PriceDifference);
        Assert.Equal(3, row.LastVersion);
    }

    [Fact]
    public async Task ProcessAsync_ShouldContinueFromPreviousDayClosePrice()
    {
        var aggregateId = Guid.NewGuid();
        await SeedPreviousDay(aggregateId, 100m);
        await SeedEvent(aggregateId, 1, 10m, DayStart.AddHours(9));
        await SeedEvent(aggregateId, 2, -20m, DayStart.AddHours(10));

        await Worker.ProcessAsync(new DailyReadModelRequested(aggregateId, Date), CancellationToken.None);

        var row = await ReadDaily(aggregateId);
        Assert.Equal(110m, row.OpenPrice);
        Assert.Equal(110m, row.HighPrice);
        Assert.Equal(90m, row.LowPrice);
        Assert.Equal(90m, row.ClosePrice);
        Assert.Equal(-20m, row.PriceDifference);
    }

    [Fact]
    public async Task ProcessAsync_ShouldWriteNothing_WhenDayHasNoEvents()
    {
        var aggregateId = Guid.NewGuid();

        await Worker.ProcessAsync(new DailyReadModelRequested(aggregateId, Date), CancellationToken.None);

        var rows = await DbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM daily_stock_data_projection WHERE aggregate_id = @AggregateId",
            new { AggregateId = aggregateId });
        Assert.Equal(0, rows);
    }

    [Fact]
    public async Task ProcessAsync_ShouldIgnoreEventsOutsideTheDayWindow()
    {
        var aggregateId = Guid.NewGuid();
        await SeedEvent(aggregateId, 1, 100m, DayStart.AddHours(-1));
        await SeedEvent(aggregateId, 2, 7m, DayStart);
        await SeedEvent(aggregateId, 3, 500m, DayStart.AddDays(1));

        await Worker.ProcessAsync(new DailyReadModelRequested(aggregateId, Date), CancellationToken.None);

        var row = await ReadDaily(aggregateId);
        Assert.Equal(7m, row.OpenPrice);
        Assert.Equal(7m, row.ClosePrice);
        Assert.Equal(0m, row.PriceDifference);
        Assert.Equal(2, row.LastVersion);
    }

    [Fact]
    public async Task ProcessAsync_ShouldIgnoreEventsOfOtherAggregates()
    {
        var aggregateId = Guid.NewGuid();
        var otherAggregateId = Guid.NewGuid();
        await SeedEvent(aggregateId, 1, 4m, DayStart.AddHours(10));
        await SeedEvent(otherAggregateId, 1, 500m, DayStart.AddHours(10));

        await Worker.ProcessAsync(new DailyReadModelRequested(aggregateId, Date), CancellationToken.None);

        var row = await ReadDaily(aggregateId);
        Assert.Equal(4m, row.ClosePrice);

        var otherRows = await DbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM daily_stock_data_projection WHERE aggregate_id = @AggregateId",
            new { AggregateId = otherAggregateId });
        Assert.Equal(0, otherRows);
    }

    [Fact]
    public async Task ProcessAsync_ShouldReadEveryEvent_WhenDayExceedsOneReaderPage()
    {
        var aggregateId = Guid.NewGuid();
        var events = Enumerable.Range(1, 250).Select(i => new
        {
            EventId = Guid.NewGuid(),
            AggregateId = aggregateId,
            Version = (long)i,
            EventType = "PriceChangedEvent",
            Payload = "{}",
            PriceChange = 1m,
            OccuredAt = DayStart.AddMinutes(i)
        }).ToList();

        await DbContext.Connection.ExecuteAsync(InsertEventSql, events);

        await Worker.ProcessAsync(new DailyReadModelRequested(aggregateId, Date), CancellationToken.None);

        var row = await ReadDaily(aggregateId);
        Assert.Equal(1m, row.OpenPrice);
        Assert.Equal(250m, row.HighPrice);
        Assert.Equal(1m, row.LowPrice);
        Assert.Equal(250m, row.ClosePrice);
        Assert.Equal(249m, row.PriceDifference);
        Assert.Equal(250, row.LastVersion);
    }

    private async Task SeedEvent(Guid aggregateId, long version, decimal priceChange, DateTimeOffset occuredAt)
    {
        await DbContext.Connection.ExecuteAsync(InsertEventSql, new
        {
            EventId = Guid.NewGuid(),
            AggregateId = aggregateId,
            Version = version,
            EventType = "PriceChangedEvent",
            Payload = "{}",
            PriceChange = priceChange,
            OccuredAt = occuredAt
        });
    }

    private async Task SeedPreviousDay(Guid aggregateId, decimal closePrice)
    {
        await DbContext.Connection.ExecuteAsync("""
                                                INSERT INTO daily_stock_data_projection
                                                    (id, aggregate_id, open_price, low_price, high_price, close_price, price_difference, last_version, date)
                                                VALUES
                                                    (@Id, @AggregateId, @Price, @Price, @Price, @Price, 0, 0, @Date)
                                                """,
            new
            {
                Id = Guid.NewGuid(),
                AggregateId = aggregateId,
                Price = closePrice,
                Date = Date.AddDays(-1)
            });
    }

    private async Task<DailyRow> ReadDaily(Guid aggregateId)
    {
        return await DbContext.Connection.QuerySingleAsync<DailyRow>("""
                                                                     SELECT open_price AS OpenPrice, low_price AS LowPrice, high_price AS HighPrice,
                                                                            close_price AS ClosePrice, price_difference AS PriceDifference,
                                                                            last_version AS LastVersion
                                                                     FROM daily_stock_data_projection
                                                                     WHERE aggregate_id = @AggregateId AND date = @Date
                                                                     """, new { AggregateId = aggregateId, Date });
    }

    private readonly record struct DailyRow(
        decimal OpenPrice,
        decimal LowPrice,
        decimal HighPrice,
        decimal ClosePrice,
        decimal PriceDifference,
        long LastVersion);
}