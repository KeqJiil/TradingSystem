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

    private const string InsertHourlySql = """
                                            INSERT INTO hourly_stock_data_projection
                                                (id, aggregate_id, open_price, low_price, high_price, close_price, price_difference, last_version, date_time)
                                            VALUES
                                                (@Id, @AggregateId, @OpenPrice, @LowPrice, @HighPrice, @ClosePrice, @PriceDifference, @LastVersion, @DateTime)
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
            new StockPriceHourlyReader(dbContext),
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
    public async Task ProcessAsync_ShouldComputeOhlcFromHourlyReadModels()
    {
        var aggregateId = Guid.NewGuid();
        await SeedHour(aggregateId, DayStart.AddHours(10), open: 10m, low: 10m, high: 15m, close: 15m);
        await SeedHour(aggregateId, DayStart.AddHours(11), open: 15m, low: 12m, high: 20m, close: 12m);
        await SeedHour(aggregateId, DayStart.AddHours(12), open: 12m, low: 9m, high: 12m, close: 9m);
        await SeedEvent(aggregateId, 3, DayStart.AddHours(12).AddMinutes(30));

        await Worker.ProcessAsync(new DailyReadModelRequested(aggregateId, Date), CancellationToken.None);

        var row = await ReadDaily(aggregateId);
        Assert.Equal(10m, row.OpenPrice);
        Assert.Equal(20m, row.HighPrice);
        Assert.Equal(9m, row.LowPrice);
        Assert.Equal(9m, row.ClosePrice);
        Assert.Equal(-1m, row.PriceDifference);
        Assert.Equal(3, row.LastVersion);
    }

    [Fact]
    public async Task ProcessAsync_ShouldWriteNothing_WhenDayHasNoHourlyReadModels()
    {
        var aggregateId = Guid.NewGuid();

        await Worker.ProcessAsync(new DailyReadModelRequested(aggregateId, Date), CancellationToken.None);

        var rows = await DbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM daily_stock_data_projection WHERE aggregate_id = @AggregateId",
            new { AggregateId = aggregateId });
        Assert.Equal(0, rows);
    }

    [Fact]
    public async Task ProcessAsync_ShouldIgnoreHourlyReadModelsOutsideTheDayWindow()
    {
        var aggregateId = Guid.NewGuid();
        await SeedHour(aggregateId, DayStart.AddHours(-1), open: 999m, low: 999m, high: 999m, close: 999m);
        await SeedHour(aggregateId, DayStart, open: 7m, low: 7m, high: 7m, close: 7m);
        await SeedHour(aggregateId, DayStart.AddHours(23), open: 7m, low: 7m, high: 7m, close: 7m);
        await SeedHour(aggregateId, DayStart.AddDays(1), open: 500m, low: 500m, high: 500m, close: 500m);
        await SeedEvent(aggregateId, 2, DayStart);

        await Worker.ProcessAsync(new DailyReadModelRequested(aggregateId, Date), CancellationToken.None);

        var row = await ReadDaily(aggregateId);
        Assert.Equal(7m, row.OpenPrice);
        Assert.Equal(7m, row.ClosePrice);
        Assert.Equal(0m, row.PriceDifference);
        Assert.Equal(2, row.LastVersion);
    }

    [Fact]
    public async Task ProcessAsync_ShouldIgnoreHourlyReadModelsOfOtherAggregates()
    {
        var aggregateId = Guid.NewGuid();
        var otherAggregateId = Guid.NewGuid();
        await SeedHour(aggregateId, DayStart.AddHours(10), open: 4m, low: 4m, high: 4m, close: 4m);
        await SeedHour(otherAggregateId, DayStart.AddHours(10), open: 500m, low: 500m, high: 500m, close: 500m);
        await SeedEvent(aggregateId, 1, DayStart.AddHours(10));

        await Worker.ProcessAsync(new DailyReadModelRequested(aggregateId, Date), CancellationToken.None);

        var row = await ReadDaily(aggregateId);
        Assert.Equal(4m, row.ClosePrice);

        var otherRows = await DbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM daily_stock_data_projection WHERE aggregate_id = @AggregateId",
            new { AggregateId = otherAggregateId });
        Assert.Equal(0, otherRows);
    }

    private async Task SeedHour(Guid aggregateId, DateTimeOffset hourStart, decimal open, decimal low, decimal high,
        decimal close)
    {
        await DbContext.Connection.ExecuteAsync(InsertHourlySql, new
        {
            Id = Guid.NewGuid(),
            AggregateId = aggregateId,
            OpenPrice = open,
            LowPrice = low,
            HighPrice = high,
            ClosePrice = close,
            PriceDifference = close - open,
            LastVersion = 0L,
            DateTime = hourStart
        });
    }

    private async Task SeedEvent(Guid aggregateId, long version, DateTimeOffset occuredAt)
    {
        await DbContext.Connection.ExecuteAsync(InsertEventSql, new
        {
            EventId = Guid.NewGuid(),
            AggregateId = aggregateId,
            Version = version,
            EventType = "PriceChangedEvent",
            Payload = "{}",
            PriceChange = 0m,
            OccuredAt = occuredAt
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
