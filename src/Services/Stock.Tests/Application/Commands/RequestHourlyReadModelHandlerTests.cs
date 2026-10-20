using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Stock.Application.Commands.RequestHourlyReadModel;
using Stock.Application.Events;
using Stock.Tests.Infrastructure;
using Stock.Tests.Infrastructure.Cron;
using Xunit;

namespace Stock.Tests.Application.Commands;

[Collection(HangfireCollection.Name)]
public class RequestHourlyReadModelHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private static readonly DateOnly Date = new(2026, 3, 5);

    private readonly TestDbContext _dbContext;
    private readonly ServiceProvider _provider;

    public RequestHourlyReadModelHandlerTests(MssqlFixture fixture)
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
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(250)]
    public async Task Handle_EnqueuesExactlyOneHourlyJobPerStock_AcrossBatchBoundaries(int stocks)
    {
        var stockIds = await CronTestHost.SeedStocksAsync(_dbContext, stocks);

        await Send(new RequestHourlyReadModelCommand(Date, 10));

        var aggregateIds = JobsFor(stockIds).Select(job => job.Event.AggregateId);
        Assert.Equal(stockIds.Order(), aggregateIds.Order());
    }

    [Fact]
    public async Task Handle_JobCarriesStockIdDateAndHourOfRequest()
    {
        var stockIds = await CronTestHost.SeedStocksAsync(_dbContext, 1);

        await Send(new RequestHourlyReadModelCommand(Date, 23));

        var job = Assert.Single(JobsFor(stockIds));
        Assert.Equal(new HourlyReadModelRequested(stockIds[0], Date, 23), job.Event);
    }

    private async Task Send(RequestHourlyReadModelCommand command)
    {
        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
    }

    private List<EnqueuedJob<HourlyReadModelRequested>> JobsFor(List<Guid> stockIds)
    {
        return CronTestHost.EnqueuedJobs<HourlyReadModelRequested>(_provider)
            .Where(job => stockIds.Contains(job.Event.AggregateId))
            .ToList();
    }
}
