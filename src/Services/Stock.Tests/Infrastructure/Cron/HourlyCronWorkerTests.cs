using System.Globalization;
using System.Text.Json;
using Dapper;
using Stock.Application.Events;
using Stock.Infrastructure.Cron;
using Stock.Tests.Infrastructure.Messaging.Consumers;
using Xunit;

namespace Stock.Tests.Infrastructure.Cron;

public class HourlyCronWorkerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly MssqlFixture _fixture;
    private TestDbContext _dbContext = null!;

    public HourlyCronWorkerTests(MssqlFixture fixture)
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
    [InlineData("2026-03-05T10:05:00+00:00", "2026-03-05", 9)]
    [InlineData("2026-03-05T10:00:00+00:00", "2026-03-05", 9)]
    [InlineData("2026-03-05T10:59:59+00:00", "2026-03-05", 9)]
    [InlineData("2026-03-05T01:00:00+00:00", "2026-03-05", 0)]
    [InlineData("2026-03-05T00:59:59+00:00", "2026-03-04", 23)]
    [InlineData("2026-03-05T00:05:00+00:00", "2026-03-04", 23)]
    [InlineData("2026-01-01T00:05:00+00:00", "2025-12-31", 23)]
    public async Task ExecuteAsync_RequestsPreviousHour_EvenWhenItBelongsToPreviousDay(string now,
        string expectedDate, int expectedHour)
    {
        var stockId = (await CronTestHost.SeedStocksAsync(_dbContext, 1)).Single();

        await CronTestHost.RunAsync<HourlyCronWorker>(_fixture.ConnectionString,
            new FakeSystemClock(DateTimeOffset.Parse(now, CultureInfo.InvariantCulture)),
            worker => worker.ExecuteAsync(CancellationToken.None));

        var payload = JsonSerializer.Deserialize<HourlyReadModelRequested>(
            await _dbContext.Connection.QuerySingleAsync<string>(
                "SELECT payload FROM outbox WHERE event_type = @EventType",
                new { EventType = nameof(HourlyReadModelRequested) }))!;
        Assert.Equal(
            new HourlyReadModelRequested(stockId, DateOnly.Parse(expectedDate, CultureInfo.InvariantCulture),
                (byte)expectedHour), payload);
    }
}
