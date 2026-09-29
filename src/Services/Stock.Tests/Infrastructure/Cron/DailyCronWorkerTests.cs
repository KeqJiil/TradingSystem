using System.Globalization;
using System.Text.Json;
using Dapper;
using Stock.Application.Events;
using Stock.Infrastructure.Cron;
using Stock.Tests.Infrastructure.Messaging.Consumers;
using Xunit;

namespace Stock.Tests.Infrastructure.Cron;

public class DailyCronWorkerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly MssqlFixture _fixture;
    private TestDbContext _dbContext = null!;

    public DailyCronWorkerTests(MssqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _dbContext = new TestDbContext(_fixture.ConnectionString);
        await _dbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(_dbContext);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Theory]
    [InlineData("2026-03-05T00:00:00+00:00", "2026-03-04")]
    [InlineData("2026-03-05T01:00:00+00:00", "2026-03-04")]
    [InlineData("2026-03-05T23:59:59+00:00", "2026-03-04")]
    [InlineData("2026-01-01T01:00:00+00:00", "2025-12-31")]
    [InlineData("2028-03-01T01:00:00+00:00", "2028-02-29")]
    [InlineData("2026-03-05T01:00:00+05:00", "2026-03-03")]
    public async Task ExecuteAsync_RequestsPreviousUtcDay(string now, string expectedDate)
    {
        var stockId = (await CronTestHost.SeedStocksAsync(_dbContext, 1)).Single();

        await Run(now);

        var payload = JsonSerializer.Deserialize<DailyReadModelRequested>(
            await _dbContext.Connection.QuerySingleAsync<string>(
                "SELECT payload FROM outbox WHERE event_type = @EventType",
                new { EventType = nameof(DailyReadModelRequested) }))!;
        Assert.Equal(new DailyReadModelRequested(stockId, DateOnly.Parse(expectedDate, CultureInfo.InvariantCulture)),
            payload);
    }

    [Fact]
    public async Task ExecuteAsync_WritesNothing_WhenThereAreNoStocks()
    {
        await Run("2026-03-05T01:00:00+00:00");

        Assert.Equal(0, await _dbContext.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM outbox"));
    }

    [Fact]
    public async Task ExecuteAsync_TagsAllRowsOfOneRunWithSameCorrelationId_AndUsesNewOneForNextRun()
    {
        await CronTestHost.SeedStocksAsync(_dbContext, 3);

        await Run("2026-03-05T01:00:00+00:00");
        var first = (await _dbContext.Connection.QueryAsync<Guid?>("SELECT DISTINCT correlation_id FROM outbox"))
            .ToList();
        await _dbContext.Connection.ExecuteAsync("DELETE FROM outbox");
        await Run("2026-03-06T01:00:00+00:00");
        var second = (await _dbContext.Connection.QueryAsync<Guid?>("SELECT DISTINCT correlation_id FROM outbox"))
            .ToList();

        var firstId = Assert.Single(first);
        var secondId = Assert.Single(second);
        Assert.NotNull(firstId);
        Assert.NotNull(secondId);
        Assert.NotEqual(firstId, secondId);
    }

    private Task Run(string now)
    {
        return CronTestHost.RunAsync<DailyCronWorker>(_fixture.ConnectionString,
            new FakeSystemClock(DateTimeOffset.Parse(now, CultureInfo.InvariantCulture)),
            worker => worker.ExecuteAsync(CancellationToken.None));
    }
}
