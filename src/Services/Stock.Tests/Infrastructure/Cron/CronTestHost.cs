using Dapper;
using Hangfire;
using Hangfire.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Stock.Application.Abstractions;
using Stock.Application.Events;
using Stock.Infrastructure.Cron;
using Stock.Infrastructure.Handlers;
using Stock.Infrastructure.Persistence;
using Stock.Presentation.Builder;

namespace Stock.Tests.Infrastructure.Cron;

internal static class CronTestHost
{
    static CronTestHost()
    {
        GlobalJobFilters.Filters.Add(new CorrelationJobFilter());
    }

    public static ServiceProvider Build(string connectionString, ISystemClock clock)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString
        });
        builder.AddResilence();
        builder.AddPersistence();
        builder.AddApplication();
        builder.AddJobs();
        builder.Services.AddSingleton(clock);
        builder.Services.AddHangfire(config => config.UseSqlServerStorage(connectionString));

        return builder.Services.BuildServiceProvider();
    }

    public static List<EnqueuedJob<TEvent>> EnqueuedJobs<TEvent>(IServiceProvider provider) where TEvent : JobEvent
    {
        var storage = provider.GetRequiredService<JobStorage>();
        using var connection = storage.GetConnection();

        return storage.GetMonitoringApi()
            .EnqueuedJobs("default", 0, 100_000)
            .Where(pair => pair.Value.Job?.Type == typeof(IJobEventProcessor<TEvent>)
                           && pair.Value.Job.Args.FirstOrDefault() is TEvent)
            .Select(pair => new EnqueuedJob<TEvent>((TEvent)pair.Value.Job.Args[0],
                SerializationHelper.Deserialize<Guid?>(connection.GetJobParameter(pair.Key, "CorrelationId"))))
            .ToList();
    }

    public static async Task<List<Guid>> SeedStocksAsync(TestDbContext dbContext, int count)
    {
        var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

        await dbContext.Connection.ExecuteAsync(
            "INSERT INTO stock_data (id, name, is_open_to_trade, trading_start_time, trading_end_time, currency) VALUES (@Id, 'Stock', 1, '09:00', '17:00', 'USD')",
            ids.Select(id => new { Id = id }));

        return ids;
    }
}

internal sealed record EnqueuedJob<TEvent>(TEvent Event, Guid? CorrelationId) where TEvent : JobEvent;
