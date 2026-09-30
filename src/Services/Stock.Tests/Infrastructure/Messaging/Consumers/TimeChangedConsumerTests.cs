using Dapper;
using Microsoft.Data.SqlClient;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Options;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Consumers;

[Collection(StockServiceCollection.Name)]
public class TimeChangedConsumerTests
{
    private readonly StockServiceHostFixture _fixture;

    public TimeChangedConsumerTests(StockServiceHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HappyPath_TimeChangedEvent_UpdatesTradingTimeAndVersion()
    {
        var aggregateId = Guid.NewGuid();
        await SeedStockAsync(aggregateId);

        ProduceTimeChange(aggregateId);

        var row = await Polling.WaitUntilAsync(() => TryGetTimeIfVersionAsync(aggregateId, 1),
            TimeSpan.FromSeconds(45));
        Assert.Equal(TimeSpan.FromHours(10), row.TradingStart);
        Assert.Equal(TimeSpan.FromHours(18), row.TradingEnd);
    }

    [Fact]
    public async Task TimeChangeArrivesBeforeStockCreated_IsAppliedOnceReadModelExists()
    {
        var aggregateId = Guid.NewGuid();
        ProduceTimeChange(aggregateId);

        await SeedStockAsync(aggregateId);

        var row = await Polling.WaitUntilAsync(() => TryGetTimeIfVersionAsync(aggregateId, 1),
            TimeSpan.FromSeconds(60));
        Assert.Equal(TimeSpan.FromHours(10), row.TradingStart);
    }

    private void ProduceTimeChange(Guid aggregateId)
    {
        _fixture.Produce(TopicNames.StockTradingTimeChanged, aggregateId,
            new TimeChangedEvent(aggregateId, new TimeOnly(10, 0), new TimeOnly(18, 0), 1));
    }

    private async Task SeedStockAsync(Guid aggregateId)
    {
        _fixture.Produce(TopicNames.StockCreated, aggregateId,
            new StockCreatedEvent(aggregateId, "AAPL", true, "USD", new TimeOnly(9, 30), new TimeOnly(16, 0)));

        await Polling.WaitUntilAsync(() => TryGetTimeIfVersionAsync(aggregateId, 0), TimeSpan.FromSeconds(45));
    }

    private async Task<TimeRow?> TryGetTimeIfVersionAsync(Guid aggregateId, long expectedVersion)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.QuerySingleOrDefaultAsync<TimeRow>("""
            SELECT "trading_start_time" AS TradingStart, "trading_end_time" AS TradingEnd,
                   "trading_time_version" AS Version
            FROM "stock_data_projection"
            WHERE "aggregate_id" = @AggregateId AND "trading_time_version" >= @ExpectedVersion
            """, new { AggregateId = aggregateId, ExpectedVersion = expectedVersion });
    }

    private record TimeRow(TimeSpan TradingStart, TimeSpan TradingEnd, long Version);
}
