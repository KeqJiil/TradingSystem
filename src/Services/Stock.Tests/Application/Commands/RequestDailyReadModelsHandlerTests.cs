using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Stock.Application.Commands.RequestDailyReadModels;
using Stock.Application.Events;
using Stock.Tests.Infrastructure;
using Stock.Tests.Infrastructure.Cron;
using Xunit;

namespace Stock.Tests.Application.Commands;

[Collection(HangfireCollection.Name)]
public class RequestDailyReadModelsHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly TestDbContext _dbContext;
    private readonly ServiceProvider _provider;

    public RequestDailyReadModelsHandlerTests(MssqlFixture fixture)
    {
        _dbContext = new TestDbContext(fixture.ConnectionString);
        _provider = CronTestHost.Build(fixture.ConnectionString, new SystemClock());
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
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(200)]
    [InlineData(250)]
    public async Task Handle_EnqueuesExactlyOneDailyJobPerStock_AcrossBatchBoundaries(int stocks)
    {
        var stockIds = await CronTestHost.SeedStocksAsync(_dbContext, stocks);

        await Send(new RequestDailyReadModelsCommand(DateTime.UtcNow));

        var aggregateIds = JobsFor(stockIds).Select(job => job.Event.AggregateId);
        Assert.Equal(stockIds.Order(), aggregateIds.Order());
    }

    [Fact]
    public async Task Handle_JobCarriesStockIdAndDateOfRequest()
    {
        var stockIds = await CronTestHost.SeedStocksAsync(_dbContext, 1);

        await Send(new RequestDailyReadModelsCommand(new DateTime(2026, 3, 5, 14, 30, 0, DateTimeKind.Utc)));

        var job = Assert.Single(JobsFor(stockIds));
        Assert.Equal(new DailyReadModelRequested(stockIds[0], new DateOnly(2026, 3, 5)), job.Event);
    }

    private async Task Send(RequestDailyReadModelsCommand command)
    {
        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
    }

    private List<EnqueuedJob<DailyReadModelRequested>> JobsFor(List<Guid> stockIds)
    {
        return CronTestHost.EnqueuedJobs<DailyReadModelRequested>(_provider)
            .Where(job => stockIds.Contains(job.Event.AggregateId))
            .ToList();
    }
}
