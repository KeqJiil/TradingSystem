using Hangfire;

namespace Stock.Infrastructure.Cron;

public static class CronBuilder
{
    public static void UseCronJobs(this WebApplication app)
    {
        var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate<DailyCronWorker>("daily-read-model",
            w => w.ExecuteAsync(CancellationToken.None), "0 1 * * *");

        recurringJobManager.AddOrUpdate<HourlyCronWorker>("hourly-read-model",
            w => w.ExecuteAsync(CancellationToken.None), "5 * * * *");

        recurringJobManager.AddOrUpdate<OutboxCleanupCronWorker>("outbox-cleanup",
            w => w.ExecuteAsync(CancellationToken.None), "0 3 * * *");
    }
}