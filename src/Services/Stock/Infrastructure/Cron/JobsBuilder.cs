using Stock.Application.Abstractions;
using Stock.Application.Events;
using Stock.Infrastructure.BackgroundWorkers;

namespace Stock.Infrastructure.Cron;

public static class JobsBuilder
{
    public static void AddJobs(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IJobEventProcessor<DailyReadModelRequested>, DailyReadModelWorker>();
        builder.Services.AddScoped<DailyCronWorker>();
        builder.Services.AddScoped<IJobEventProcessor<HourlyReadModelRequested>, HourlyReadModelWorker>();
        builder.Services.AddScoped<HourlyCronWorker>();
    }
}
