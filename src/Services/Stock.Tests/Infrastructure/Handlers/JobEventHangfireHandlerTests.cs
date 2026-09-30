using Hangfire;
using Hangfire.Common;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stock.Application.Abstractions;
using Stock.Application.Events;
using Stock.Infrastructure;
using Stock.Infrastructure.BackgroundWorkers;
using Stock.Infrastructure.Handlers;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Persistence;
using Stock.Presentation.Builder;
using Xunit;

namespace Stock.Tests.Infrastructure.Handlers;

public class JobEventHangfireHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private static readonly DateOnly Date = new(2026, 3, 5);

    private readonly ServiceProvider _provider;
    private readonly JobStorage _storage;

    public JobEventHangfireHandlerTests(MssqlFixture fixture)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fixture.ConnectionString
        });
        builder.AddResilence();
        builder.AddPersistence();
        builder.AddApplication();
        builder.AddMessaging();
        builder.Services.AddHangfire(config => config.UseSqlServerStorage(fixture.ConnectionString));

        _provider = builder.Services.BuildServiceProvider();
        _storage = _provider.GetRequiredService<JobStorage>();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Publish_DailyReadModelRequested_EnqueuesSingleJobForDailyProcessor()
    {
        var @event = new DailyReadModelRequested(Guid.NewGuid(), Date);

        await Publish(@event);

        var job = Assert.Single(EnqueuedJobsFor(@event.AggregateId));
        Assert.Equal(typeof(IJobEventProcessor<DailyReadModelRequested>), job.Type);
        Assert.Equal(nameof(IJobEventProcessor<DailyReadModelRequested>.ProcessAsync), job.Method.Name);
        Assert.Equal(@event, job.Args[0]);
    }

    [Fact]
    public async Task Publish_HourlyReadModelRequested_EnqueuesSingleJobForHourlyProcessor()
    {
        var @event = new HourlyReadModelRequested(Guid.NewGuid(), Date, 10);

        await Publish(@event);

        var job = Assert.Single(EnqueuedJobsFor(@event.AggregateId));
        Assert.Equal(typeof(IJobEventProcessor<HourlyReadModelRequested>), job.Type);
        Assert.Equal(@event, job.Args[0]);
    }

    [Fact]
    public async Task Publish_SameEventTwice_EnqueuesTwoJobs()
    {
        var @event = new DailyReadModelRequested(Guid.NewGuid(), Date);

        await Publish(@event);
        await Publish(@event);

        Assert.Equal(2, EnqueuedJobsFor(@event.AggregateId).Count);
    }

    [Fact]
    public async Task JobProcessors_ResolveToReadModelWorkers()
    {
        await using var scope = _provider.CreateAsyncScope();

        Assert.IsType<DailyReadModelWorker>(
            scope.ServiceProvider.GetRequiredService<IJobEventProcessor<DailyReadModelRequested>>());
        Assert.IsType<HourlyReadModelWorker>(
            scope.ServiceProvider.GetRequiredService<IJobEventProcessor<HourlyReadModelRequested>>());
    }

    [Fact]
    public void CorrelationJobFilter_StoresCurrentCorrelationIdAsJobParameter()
    {
        var correlationId = Guid.NewGuid();
        var client = new BackgroundJobClient(_storage, new JobFilterCollection { new CorrelationJobFilter() });
        var @event = new DailyReadModelRequested(Guid.NewGuid(), Date);

        CorrelationContext.CorrelationId = correlationId;
        string jobId;
        try
        {
            jobId = client.Enqueue<IJobEventProcessor<DailyReadModelRequested>>(p =>
                p.ProcessAsync(@event, CancellationToken.None));
        }
        finally
        {
            CorrelationContext.CorrelationId = null;
        }

        using var connection = _storage.GetConnection();
        Assert.Equal(correlationId,
            SerializationHelper.Deserialize<Guid?>(connection.GetJobParameter(jobId, "CorrelationId")));
    }

    private async Task Publish(JobEvent @event)
    {
        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Publish(@event);
    }

    private List<Job> EnqueuedJobsFor(Guid aggregateId)
    {
        return _storage.GetMonitoringApi()
            .EnqueuedJobs("default", 0, 1000)
            .Select(pair => pair.Value.Job)
            .Where(job => job?.Args.FirstOrDefault() is JobEvent e && e.AggregateId == aggregateId)
            .ToList();
    }
}
