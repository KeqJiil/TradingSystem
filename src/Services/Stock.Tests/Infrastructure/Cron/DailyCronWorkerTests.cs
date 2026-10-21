using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Stock.Application.Events;
using Stock.Infrastructure.Cron;
using Xunit;

namespace Stock.Tests.Infrastructure.Cron;

[Collection(HangfireCollection.Name)]
public class DailyCronWorkerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly FakeSystemClock _clock = new(default);
    private readonly TestDbContext _dbContext;
    private readonly ServiceProvider _provider;

    public DailyCronWorkerTests(MssqlFixture fixture)
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
    [InlineData("2026-03-05T00:00:00+00:00", "2026-03-04")]
    [InlineData("2026-03-05T01:00:00+00:00", "2026-03-04")]
    [InlineData("2026-03-05T23:59:59+00:00", "2026-03-04")]
    [InlineData("2026-01-01T01:00:00+00:00", "2025-12-31")]
    [InlineData("2028-03-01T01:00:00+00:00", "2028-02-29")]
    [InlineData("2026-03-05T01:00:00+05:00", "2026-03-03")]
    public async Task ExecuteAsync_RequestsPreviousUtcDay(string now, string expectedDate)
    {
        var stockIds = await CronTestHost.SeedStocksAsync(_dbContext, 1);

        await Run(now);

        var job = Assert.Single(JobsFor(stockIds));
        Assert.Equal(new DailyReadModelRequested(stockIds[0], DateOnly.Parse(expectedDate, CultureInfo.InvariantCulture)),
            job.Event);
    }

    [Fact]
    public async Task ExecuteAsync_EnqueuesNothing_WhenThereAreNoStocks()
    {
        var before = CronTestHost.EnqueuedJobs<DailyReadModelRequested>(_provider).Count;

        await Run("2026-03-05T01:00:00+00:00");

        Assert.Equal(before, CronTestHost.EnqueuedJobs<DailyReadModelRequested>(_provider).Count);
    }

    [Fact]
    public async Task ExecuteAsync_TagsAllJobsOfOneRunWithSameCorrelationId_AndUsesNewOneForNextRun()
    {
        var stockIds = await CronTestHost.SeedStocksAsync(_dbContext, 3);

        await Run("2026-03-05T01:00:00+00:00");
        await Run("2026-03-06T01:00:00+00:00");

        var runs = JobsFor(stockIds).GroupBy(job => job.Event.Date)
            .ToDictionary(g => g.Key, g => g.Select(job => job.CorrelationId).Distinct().ToList());
        var firstId = Assert.Single(runs[new DateOnly(2026, 3, 4)]);
        var secondId = Assert.Single(runs[new DateOnly(2026, 3, 5)]);
        Assert.NotNull(firstId);
        Assert.NotNull(secondId);
        Assert.NotEqual(firstId, secondId);
    }

    private async Task Run(string now)
    {
        _clock.UtcNow = DateTimeOffset.Parse(now, CultureInfo.InvariantCulture);
        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DailyCronWorker>().ExecuteAsync(CancellationToken.None);
    }

    private List<EnqueuedJob<DailyReadModelRequested>> JobsFor(List<Guid> stockIds)
    {
        return CronTestHost.EnqueuedJobs<DailyReadModelRequested>(_provider)
            .Where(job => stockIds.Contains(job.Event.AggregateId))
            .ToList();
    }
}
