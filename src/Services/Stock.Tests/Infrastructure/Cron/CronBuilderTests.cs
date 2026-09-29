using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Stock.Infrastructure.Cron;
using Stock.Infrastructure.Messaging;
using Xunit;

namespace Stock.Tests.Infrastructure.Cron;

public class CronBuilderTests : IClassFixture<MssqlFixture>
{
    private readonly MssqlFixture _fixture;

    public CronBuilderTests(MssqlFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<string, string, Type> ExpectedJobs => new()
    {
        { "daily-read-model", "0 1 * * *", typeof(DailyCronWorker) },
        { "hourly-read-model", "5 * * * *", typeof(HourlyCronWorker) },
        { "outbox-cleanup", "0 3 * * *", typeof(OutboxCleanupCronWorker) }
    };

    [Theory]
    [MemberData(nameof(ExpectedJobs))]
    public async Task UseCronJobs_RegistersUtcRecurringJob_ThatResolvesItsWorker(string id, string cron, Type worker)
    {
        await using var app = BuildApp();

        app.UseCronJobs();

        var job = Assert.Single(GetRecurringJobs(app), j => j.Id == id);
        Assert.Equal(cron, job.Cron);
        Assert.Equal(TimeZoneInfo.Utc.Id, job.TimeZoneId);
        Assert.NotNull(job.NextExecution);
        Assert.NotNull(job.Job);
        Assert.Equal(worker, job.Job.Type);
        Assert.Equal(nameof(DailyCronWorker.ExecuteAsync), job.Job.Method.Name);

        await using var scope = app.Services.CreateAsyncScope();
        Assert.IsType(worker, scope.ServiceProvider.GetRequiredService(job.Job.Type));
    }

    [Fact]
    public async Task UseCronJobs_CalledTwice_KeepsSingleJobAndOverwritesOutdatedCron()
    {
        await using var app = BuildApp();
        app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<DailyCronWorker>("daily-read-model",
            w => w.ExecuteAsync(CancellationToken.None), "0 5 * * *");

        app.UseCronJobs();
        app.UseCronJobs();

        var job = Assert.Single(GetRecurringJobs(app), j => j.Id == "daily-read-model");
        Assert.Equal("0 1 * * *", job.Cron);
    }

    private WebApplication BuildApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _fixture.ConnectionString
        });
        builder.AddMessaging();
        builder.Services.AddSingleton<ISystemClock, SystemClock>();
        builder.Services.AddHangfire(config => config.UseSqlServerStorage(_fixture.ConnectionString));
        return builder.Build();
    }

    private static List<RecurringJobDto> GetRecurringJobs(WebApplication app)
    {
        using var connection = app.Services.GetRequiredService<JobStorage>().GetConnection();
        return connection.GetRecurringJobs();
    }
}
