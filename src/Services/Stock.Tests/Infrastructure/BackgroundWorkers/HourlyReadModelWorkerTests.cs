using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Stock.Application.Events;
using Stock.Infrastructure.BackgroundWorkers;
using Stock.Infrastructure.Persistence.Implementations;
using Xunit;

namespace Stock.Tests.Infrastructure.BackgroundWorkers;

public class HourlyReadModelWorkerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private static readonly DateOnly Date = new(2026, 3, 5);
    private static readonly DateTimeOffset DayStart = new(2026, 3, 5, 0, 0, 0, TimeSpan.Zero);

    private const string InsertEventSql = """
                                          INSERT INTO events_store (event_id, aggregate_id, version, event_type, payload, price_change, occured_at)
                                          VALUES (@EventId, @AggregateId, @Version, @EventType, @Payload, @PriceChange, @OccuredAt)
                                          """;

    private TestDbContext DbContext { get; init; }
    private HourlyReadModelWorker Worker { get; init; }

    public HourlyReadModelWorkerTests(MssqlFixture mssqlFixture)
    {
        var dbContext = new TestDbContext(mssqlFixture.ConnectionString);
        DbContext = dbContext;
        Worker = new HourlyReadModelWorker(
            NullLogger<HourlyReadModelWorker>.Instance,
            new StockEventStoreReader(dbContext),
            new StockPriceHourlyReader(dbContext),
            new StockHourlyReadModelWriter(dbContext));
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
    public async Task ProcessAsync_ShouldComputeOhlcFromCumulativePriceInsideTheHour()
    {
        var aggregateId = Guid.NewGuid();
        await SeedEvent(aggregateId, 1, 5m, At(10, 5));
        await SeedEvent(aggregateId, 2, -2m, At(10, 20));
        await SeedEvent(aggregateId, 3, 4m, At(10, 40));

        await Process(aggregateId, 10);

        var row = await ReadHour(aggregateId, 10);
        Assert.Equal(5m, row.OpenPrice);
        Assert.Equal(3m, row.LowPrice);
        Assert.Equal(7m, row.HighPrice);
        Assert.Equal(7m, row.ClosePrice);
        Assert.Equal(2m, row.PriceDifference);
        Assert.Equal(3, row.LastVersion);
    }

    [Fact]
    public async Task ProcessAsync_ShouldIncludeEventAtHourStart_AndExcludeEventAtHourEnd()
    {
        var aggregateId = Guid.NewGuid();
        await SeedEvent(aggregateId, 1, 1m, At(10, 0));
        await SeedEvent(aggregateId, 2, 2m, At(10, 59).AddSeconds(59));
        await SeedEvent(aggregateId, 3, 100m, At(11, 0));

        await Process(aggregateId, 10);

        var row = await ReadHour(aggregateId, 10);
        Assert.Equal(1m, row.OpenPrice);
        Assert.Equal(3m, row.ClosePrice);
        Assert.Equal(3m, row.HighPrice);
        Assert.Equal(2, row.LastVersion);
    }

    [Fact]
    public async Task ProcessAsync_ShouldSeedFromAllEarlierEvents_WhenNoPreviousHourExists()
    {
        var aggregateId = Guid.NewGuid();
        await SeedEvent(aggregateId, 1, 10m, At(8, 10));
        await SeedEvent(aggregateId, 2, -3m, At(9, 10));
        await SeedEvent(aggregateId, 3, 1m, At(10, 10));

        await Process(aggregateId, 10);

        var row = await ReadHour(aggregateId, 10);
        Assert.Equal(8m, row.OpenPrice);
        Assert.Equal(8m, row.ClosePrice);
        Assert.Equal(3, row.LastVersion);
    }

    [Fact]
    public async Task ProcessAsync_ShouldProduceSameResult_WhenHoursAreProcessedOutOfOrder()
    {
        var aggregateId = Guid.NewGuid();
        await SeedEvent(aggregateId, 1, 5m, At(10, 10));
        await SeedEvent(aggregateId, 2, 3m, At(10, 30));
        await SeedEvent(aggregateId, 3, 2m, At(11, 10));
        await SeedEvent(aggregateId, 4, -4m, At(11, 30));
        await SeedEvent(aggregateId, 5, 10m, At(12, 10));

        await Process(aggregateId, 10);
        await Process(aggregateId, 12);
        await Process(aggregateId, 11);

        var hour10 = await ReadHour(aggregateId, 10);
        var hour11 = await ReadHour(aggregateId, 11);
        var hour12 = await ReadHour(aggregateId, 12);
        Assert.Equal((5m, 5m, 8m, 8m), (hour10.OpenPrice, hour10.LowPrice, hour10.HighPrice, hour10.ClosePrice));
        Assert.Equal((10m, 6m, 10m, 6m), (hour11.OpenPrice, hour11.LowPrice, hour11.HighPrice, hour11.ClosePrice));
        Assert.Equal((16m, 16m, 16m, 16m), (hour12.OpenPrice, hour12.LowPrice, hour12.HighPrice, hour12.ClosePrice));
    }

    [Fact]
    public async Task ProcessAsync_ShouldWriteNothing_WhenHourHasNoEvents()
    {
        var aggregateId = Guid.NewGuid();
        await SeedEvent(aggregateId, 1, 5m, At(9, 10));

        await Process(aggregateId, 10);

        Assert.Equal(0, await CountHours(aggregateId));
    }

    [Fact]
    public async Task ProcessAsync_ShouldWriteSingleRow_WhenRunTwiceForSameHour()
    {
        var aggregateId = Guid.NewGuid();
        await SeedEvent(aggregateId, 1, 5m, At(10, 10));

        await Process(aggregateId, 10);
        await Process(aggregateId, 10);

        Assert.Equal(1, await CountHours(aggregateId));
        Assert.Equal(5m, (await ReadHour(aggregateId, 10)).ClosePrice);
    }

    private static DateTimeOffset At(int hour, int minute)
    {
        return DayStart.AddHours(hour).AddMinutes(minute);
    }

    private Task Process(Guid aggregateId, byte hour)
    {
        return Worker.ProcessAsync(new HourlyReadModelRequested(aggregateId, Date, hour), CancellationToken.None);
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

    private Task<int> CountHours(Guid aggregateId)
    {
        return DbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM hourly_stock_data_projection WHERE aggregate_id = @AggregateId",
            new { AggregateId = aggregateId });
    }

    private async Task<HourlyRow> ReadHour(Guid aggregateId, int hour)
    {
        return await DbContext.Connection.QuerySingleAsync<HourlyRow>("""
                                                                      SELECT open_price AS OpenPrice, low_price AS LowPrice, high_price AS HighPrice,
                                                                             close_price AS ClosePrice, price_difference AS PriceDifference,
                                                                             last_version AS LastVersion
                                                                      FROM hourly_stock_data_projection
                                                                      WHERE aggregate_id = @AggregateId AND date_time = @HourStart
                                                                      """,
            new { AggregateId = aggregateId, HourStart = DayStart.AddHours(hour) });
    }

    private readonly record struct HourlyRow(
        decimal OpenPrice,
        decimal LowPrice,
        decimal HighPrice,
        decimal ClosePrice,
        decimal PriceDifference,
        long LastVersion);
}
