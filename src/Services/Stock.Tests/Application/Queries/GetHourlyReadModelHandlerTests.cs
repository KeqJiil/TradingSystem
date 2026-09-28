using Dapper;
using Stock.Application.Queries.GetHourlyReadModel;
using Stock.Application.Queries.GetHourReadModel;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Queries;

public class GetHourlyReadModelHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset From = new(2026, 3, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 3, 5, 17, 0, 0, TimeSpan.Zero);

    private readonly TestDbContext _dbContext;
    private readonly GetHourlyReadModelHandler _rangeHandler;
    private readonly GetHourReadModelHandler _hourHandler;

    public GetHourlyReadModelHandlerTests(MssqlFixture fixture)
    {
        _dbContext = new TestDbContext(fixture.ConnectionString);
        var reader = new StockPriceHourlyReader(_dbContext);
        _rangeHandler = new GetHourlyReadModelHandler(reader);
        _hourHandler = new GetHourReadModelHandler(reader);
    }

    public async Task InitializeAsync()
    {
        await _dbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(_dbContext);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsHoursFromInclusiveToExclusive_OrderedByTime()
    {
        var stockId = Guid.NewGuid();
        await SeedHour(stockId, From.AddHours(-1), open: 1m, close: 1m);
        await SeedHour(stockId, From.AddHours(3), open: 12m, close: 15m);
        await SeedHour(stockId, From, open: 10m, close: 12m);
        await SeedHour(stockId, To, open: 99m, close: 99m);
        await SeedHour(Guid.NewGuid(), From.AddHours(1), open: 50m, close: 50m);

        var result = await _rangeHandler.Handle(new GetHourlyReadModelQuery(stockId, From, To), CancellationToken.None);

        Assert.Equal(stockId, result.AggregateId);
        var hours = result.PriceChanges.ToList();
        Assert.Equal([From, From.AddHours(3)], hours.Select(h => h.DateTime));
        Assert.Equal([2m, 3m], hours.Select(h => h.Difference));
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoHoursInRange()
    {
        var stockId = Guid.NewGuid();
        await SeedHour(stockId, To, open: 1m, close: 2m);

        var result = await _rangeHandler.Handle(new GetHourlyReadModelQuery(stockId, From, To), CancellationToken.None);

        Assert.Empty(result.PriceChanges);
    }

    [Fact]
    public async Task HandleHour_ReturnsExactHour_AndNullForMissingHour()
    {
        var stockId = Guid.NewGuid();
        await SeedHour(stockId, From, open: 10m, close: 12m);

        var hour = await _hourHandler.Handle(new GetHourReadModelQuery(stockId, From), CancellationToken.None);
        var missing = await _hourHandler.Handle(new GetHourReadModelQuery(stockId, From.AddHours(1)),
            CancellationToken.None);

        Assert.NotNull(hour);
        Assert.Equal((10m, 12m, From), (hour.Value.OpenPrice, hour.Value.ClosePrice, hour.Value.DateTime));
        Assert.Null(missing);
    }

    private async Task SeedHour(Guid aggregateId, DateTimeOffset hourStart, decimal open, decimal close)
    {
        await _dbContext.Connection.ExecuteAsync("""
            INSERT INTO hourly_stock_data_projection
                (id, aggregate_id, open_price, low_price, high_price, close_price, price_difference, last_version, date_time)
            VALUES
                (NEWID(), @AggregateId, @Open, @Low, @High, @Close, @Close - @Open, 1, @HourStart)
            """,
            new
            {
                AggregateId = aggregateId,
                Open = open,
                Low = Math.Min(open, close),
                High = Math.Max(open, close),
                Close = close,
                HourStart = hourStart
            });
    }
}
