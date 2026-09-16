using Dapper;
using Stock.Application.Abstractions;
using Stock.Application.Commands.ReplayReadModel;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Stock.Tests.Infrastructure.Messaging.Consumers;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class ReplayReadModelHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeSystemClock _clock = new(Now);
    private IStockEventStoreReader EventStoreReader { get; init; }
    private IStockReader ReadModelReader { get; init; }
    private IStockReadModelWriter Writer { get; init; }
    private ReplayReadModelHandler Handler { get; init; }
    private TestDbContext DbContext { get; init; }

    public ReplayReadModelHandlerTests(MssqlFixture mssqlFixture)
    {
        var dbContext = new TestDbContext(mssqlFixture.ConnectionString);
        DbContext = dbContext;
        EventStoreReader = new StockEventStoreReader(dbContext);
        ReadModelReader = new StockReader(dbContext);
        Writer = new StockReadModelWriter(dbContext);
        Handler = new ReplayReadModelHandler(EventStoreReader, ReadModelReader, _clock, Writer);
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
    public async Task Handle_ShouldApplyEveryEventAfterProjectionVersion_AndMoveVersionToNewestEvent()
    {
        var aggregateId = Guid.NewGuid();
        await SeedProjection(aggregateId, price: 100m, version: 5);
        await SeedEvent(aggregateId, version: 6, priceChange: 1m);
        await SeedEvent(aggregateId, version: 7, priceChange: 2m);
        await SeedEvent(aggregateId, version: 8, priceChange: 3m);

        await Handler.Handle(new ReplayReadModelCommand(aggregateId, 8), CancellationToken.None);

        var projection = await ReadProjection(aggregateId);
        Assert.Equal(106m, projection.Price);
        Assert.Equal(8, projection.Version);
    }

    [Fact]
    public async Task Handle_ShouldApplyFailedEvent_WhenItIsTheOnlyMissingOne()
    {
        var aggregateId = Guid.NewGuid();
        await SeedProjection(aggregateId, price: 100m, version: 5);
        await SeedEvent(aggregateId, version: 6, priceChange: 7m);

        await Handler.Handle(new ReplayReadModelCommand(aggregateId, 6), CancellationToken.None);

        var projection = await ReadProjection(aggregateId);
        Assert.Equal(107m, projection.Price);
        Assert.Equal(6, projection.Version);
    }

    [Fact]
    public async Task Handle_ShouldCloseGapOfSeveralEvents_WhenProjectionIsFarBehind()
    {
        var aggregateId = Guid.NewGuid();
        await SeedProjection(aggregateId, price: 100m, version: 1);
        await SeedEvent(aggregateId, version: 2, priceChange: 1m);
        await SeedEvent(aggregateId, version: 3, priceChange: 2m);
        await SeedEvent(aggregateId, version: 4, priceChange: 4m);

        await Handler.Handle(new ReplayReadModelCommand(aggregateId, 4), CancellationToken.None);

        var projection = await ReadProjection(aggregateId);
        Assert.Equal(107m, projection.Price);
        Assert.Equal(4, projection.Version);
    }

    [Fact]
    public async Task Handle_ShouldSumNegativeAndPositiveChanges()
    {
        var aggregateId = Guid.NewGuid();
        await SeedProjection(aggregateId, price: 100m, version: 0);
        await SeedEvent(aggregateId, version: 1, priceChange: 10.5m);
        await SeedEvent(aggregateId, version: 2, priceChange: -4.25m);
        await SeedEvent(aggregateId, version: 3, priceChange: -1.25m);

        await Handler.Handle(new ReplayReadModelCommand(aggregateId, 3), CancellationToken.None);

        var projection = await ReadProjection(aggregateId);
        Assert.Equal(105m, projection.Price);
        Assert.Equal(3, projection.Version);
    }

    [Fact]
    public async Task Handle_ShouldNotApplyEventsNewerThanMaxVersion()
    {
        var aggregateId = Guid.NewGuid();
        await SeedProjection(aggregateId, price: 100m, version: 0);
        await SeedEvent(aggregateId, version: 1, priceChange: 1m);
        await SeedEvent(aggregateId, version: 2, priceChange: 2m);
        await SeedEvent(aggregateId, version: 3, priceChange: 3m);

        await Handler.Handle(new ReplayReadModelCommand(aggregateId, 2), CancellationToken.None);

        var projection = await ReadProjection(aggregateId);
        Assert.Equal(103m, projection.Price);
        Assert.Equal(2, projection.Version);
    }

    [Fact]
    public async Task Handle_ShouldNotTouchProjection_WhenStoreHasNoEventsForAggregate()
    {
        var aggregateId = Guid.NewGuid();
        await SeedProjection(aggregateId, price: 100m, version: 5);

        await Handler.Handle(new ReplayReadModelCommand(aggregateId, 6), CancellationToken.None);

        var projection = await ReadProjection(aggregateId);
        Assert.Equal(100m, projection.Price);
        Assert.Equal(5, projection.Version);
    }

    [Fact]
    public async Task Handle_ShouldNotTouchProjection_WhenAllEventsAreAlreadyApplied()
    {
        var aggregateId = Guid.NewGuid();
        await SeedProjection(aggregateId, price: 100m, version: 3);
        await SeedEvent(aggregateId, version: 1, priceChange: 5m);
        await SeedEvent(aggregateId, version: 2, priceChange: 5m);
        await SeedEvent(aggregateId, version: 3, priceChange: 5m);

        await Handler.Handle(new ReplayReadModelCommand(aggregateId, 3), CancellationToken.None);

        var projection = await ReadProjection(aggregateId);
        Assert.Equal(100m, projection.Price);
        Assert.Equal(3, projection.Version);
    }

    [Fact]
    public async Task Handle_ShouldNotTouchProjection_WhenMaxVersionIsBehindProjection()
    {
        var aggregateId = Guid.NewGuid();
        await SeedProjection(aggregateId, price: 100m, version: 5);
        await SeedEvent(aggregateId, version: 6, priceChange: 9m);

        await Handler.Handle(new ReplayReadModelCommand(aggregateId, 4), CancellationToken.None);

        var projection = await ReadProjection(aggregateId);
        Assert.Equal(100m, projection.Price);
        Assert.Equal(5, projection.Version);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreEventsThatOccuredAfterClockNow()
    {
        var aggregateId = Guid.NewGuid();
        await SeedProjection(aggregateId, price: 100m, version: 0);
        await SeedEvent(aggregateId, version: 1, priceChange: 2m, occuredAt: Now.AddMinutes(-1));
        await SeedEvent(aggregateId, version: 2, priceChange: 500m, occuredAt: Now.AddMinutes(1));

        await Handler.Handle(new ReplayReadModelCommand(aggregateId, 2), CancellationToken.None);

        var projection = await ReadProjection(aggregateId);
        Assert.Equal(102m, projection.Price);
        Assert.Equal(1, projection.Version);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreEventThatOccuredExactlyAtClockNow()
    {
        var aggregateId = Guid.NewGuid();
        await SeedProjection(aggregateId, price: 100m, version: 0);
        await SeedEvent(aggregateId, version: 1, priceChange: 500m, occuredAt: Now);

        await Handler.Handle(new ReplayReadModelCommand(aggregateId, 1), CancellationToken.None);

        var projection = await ReadProjection(aggregateId);
        Assert.Equal(100m, projection.Price);
        Assert.Equal(0, projection.Version);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreEventsOfOtherAggregates()
    {
        var aggregateId = Guid.NewGuid();
        var otherAggregateId = Guid.NewGuid();
        await SeedProjection(aggregateId, price: 100m, version: 0);
        await SeedProjection(otherAggregateId, price: 100m, version: 0);
        await SeedEvent(aggregateId, version: 1, priceChange: 3m);
        await SeedEvent(otherAggregateId, version: 2, priceChange: 500m);

        await Handler.Handle(new ReplayReadModelCommand(aggregateId, 2), CancellationToken.None);

        var projection = await ReadProjection(aggregateId);
        var otherProjection = await ReadProjection(otherAggregateId);
        Assert.Equal(103m, projection.Price);
        Assert.Equal(1, projection.Version);
        Assert.Equal(100m, otherProjection.Price);
        Assert.Equal(0, otherProjection.Version);
    }

    [Fact]
    public async Task Handle_ShouldBeIdempotent_WhenRunTwiceForSameCommand()
    {
        var aggregateId = Guid.NewGuid();
        await SeedProjection(aggregateId, price: 100m, version: 0);
        await SeedEvent(aggregateId, version: 1, priceChange: 4m);
        await SeedEvent(aggregateId, version: 2, priceChange: 6m);

        var command = new ReplayReadModelCommand(aggregateId, 2);
        await Handler.Handle(command, CancellationToken.None);
        await Handler.Handle(command, CancellationToken.None);

        var projection = await ReadProjection(aggregateId);
        Assert.Equal(110m, projection.Price);
        Assert.Equal(2, projection.Version);
    }

    [Fact]
    public async Task Handle_ShouldNotCreateProjection_WhenItDoesNotExistYet()
    {
        var aggregateId = Guid.NewGuid();
        await SeedEvent(aggregateId, version: 1, priceChange: 4m);

        await Handler.Handle(new ReplayReadModelCommand(aggregateId, 1), CancellationToken.None);

        var rows = await DbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM stock_data_projection WHERE aggregate_id = @AggregateId",
            new { AggregateId = aggregateId });
        Assert.Equal(0, rows);
    }

    private async Task SeedProjection(Guid aggregateId, decimal price, long version)
    {
        await DbContext.Connection.ExecuteAsync("""
            INSERT INTO stock_data_projection (
                aggregate_id, name, is_open_to_trade, price, version, trading_start_time, trading_end_time, currency
            ) VALUES (
                @AggregateId, @Name, @IsOpenToTrade, @Price, @Version, @TradingStartTime, @TradingEndTime, @Currency
            )
            """,
            new
            {
                AggregateId = aggregateId,
                Name = "TestStock",
                IsOpenToTrade = true,
                Price = price,
                Version = version,
                TradingStartTime = new TimeOnly(9, 0),
                TradingEndTime = new TimeOnly(17, 0),
                Currency = "USD"
            });
    }

    private async Task SeedEvent(Guid aggregateId, long version, decimal priceChange, DateTimeOffset? occuredAt = null)
    {
        await DbContext.Connection.ExecuteAsync("""
            INSERT INTO events_store (event_id, aggregate_id, version, event_type, payload, price_change, occured_at)
            VALUES (@EventId, @AggregateId, @Version, @EventType, @Payload, @PriceChange, @OccuredAt)
            """,
            new
            {
                EventId = Guid.NewGuid(),
                AggregateId = aggregateId,
                Version = version,
                EventType = "PriceChangedEvent",
                Payload = "{}",
                PriceChange = priceChange,
                OccuredAt = occuredAt ?? Now.AddHours(-1)
            });
    }

    private async Task<ProjectionRow> ReadProjection(Guid aggregateId)
    {
        return await DbContext.Connection.QuerySingleAsync<ProjectionRow>("""
            SELECT price AS Price, version AS Version
            FROM stock_data_projection
            WHERE aggregate_id = @AggregateId
            """, new { AggregateId = aggregateId });
    }

    private readonly record struct ProjectionRow(decimal Price, long Version);
}
