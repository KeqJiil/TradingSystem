using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Stock.Application.Events;
using Stock.Infrastructure.Cron;
using Xunit;

namespace Stock.Tests.Infrastructure.Cron;

[Collection(HangfireCollection.Name)]
public class HourlyCronWorkerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly FakeSystemClock _clock = new(default);
    private readonly TestDbContext _dbContext;
    private readonly ServiceProvider _provider;

    public HourlyCronWorkerTests(MssqlFixture fixture)
    {
        _dbContext = new TestDbContext(fixture.ConnectionString);
        _provider = CronTestHost.Build(fixture.ConnectionString, _clock);
    }

    public async Task InitializeAsync()
    {
        await _dbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(_dbContext);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
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
        var stockIds = await CronTestHost.SeedStocksAsync(_dbContext, 1);

        _clock.UtcNow = DateTimeOffset.Parse(now, CultureInfo.InvariantCulture);
        await using (var scope = _provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<HourlyCronWorker>().ExecuteAsync(CancellationToken.None);

        var job = Assert.Single(CronTestHost.EnqueuedJobs<HourlyReadModelRequested>(_provider),
            job => job.Event.AggregateId == stockIds[0]);
        Assert.Equal(
            new HourlyReadModelRequested(stockIds[0], DateOnly.Parse(expectedDate, CultureInfo.InvariantCulture),
                (byte)expectedHour), job.Event);
    }
}
