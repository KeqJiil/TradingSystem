using Dapper;
using Stock.Application.Abstractions;
using Stock.Application.Queries.GetHourlyReadModel;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Queries;

public class GetHourlyReadModelHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private IStockPriceHistoryReader Reader { get; init; }
    private GetHourlyReadModelHandler Handler { get; init; }
    private MssqlFixture MssqlFixture { get; init; }
    private TestDbContext DbContext { get; init; }

    public GetHourlyReadModelHandlerTests(MssqlFixture mssqlFixture)
    {
        MssqlFixture = mssqlFixture;
        var dbContext = new TestDbContext(MssqlFixture.ConnectionString);
        DbContext = dbContext;
        Reader = new StockPriceHistoryReader(dbContext);
        Handler = new GetHourlyReadModelHandler(Reader);
    }

    public async Task InitializeAsync()
    {
        await DbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await StockTestSchema.EnsureEventsStoreCreatedAsync(DbContext);
    }

    public async Task DisposeAsync()
    {
        await DbContext.Connection.ExecuteAsync("DROP TABLE IF EXISTS events_store");
        await DbContext.Connection.CloseAsync();
    }

    [Fact]
    public async Task Handle_ShouldReturnOnlyPriceChanges_WithinRequestedRange()
    {
        var stockId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 3, 5, 9, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 3, 5, 17, 0, 0, TimeSpan.Zero);
        await SeedEvent(stockId, from.AddHours(-1), 100m); 
        await SeedEvent(stockId, from.AddHours(1), 1.5m); 
        await SeedEvent(stockId, to, 200m); 
        await SeedEvent(Guid.NewGuid(), from.AddHours(1), 300m);

        var result = await Handler.Handle(
            new GetHourlyReadModelQuery(stockId, from, to, new TimeOnly(1, 0)), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(stockId, result.AggregateId);
        var priceChange = Assert.Single(result.PriceChanges);
        Assert.Equal(1.5m, priceChange.PriceDifference);
    }

    private async Task SeedEvent(Guid aggregateId, DateTimeOffset occuredAt, decimal priceChange)
    {
        await DbContext.Connection.ExecuteAsync("""
            INSERT INTO events_store
                (event_id, aggregate_id, version, event_type, payload, price_change, occured_at)
            VALUES
                (@EventId, @AggregateId, @Version, @EventType, @Payload, @PriceChange, @OccuredAt)
            """,
            new
            {
                EventId = Guid.NewGuid(),
                AggregateId = aggregateId,
                Version = 1L,
                EventType = "PriceChangedEvent",
                Payload = "{}",
                PriceChange = priceChange,
                OccuredAt = occuredAt
            });
    }
}
