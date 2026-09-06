using Hangfire;

namespace Stock.Infrastructure.Cron;

public static class CronBuilder
{
    public static void UseCronJobs(this WebApplication app)
    {
        var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate<DailyCronWorker>(
            "daily-read-model",
            worker => worker.ExecuteAsync(CancellationToken.None),
            Hangfire.Cron.Daily());
    }
}
