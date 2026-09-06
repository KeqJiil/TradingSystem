using Stock.Application.Abstractions;
using Stock.Application.Events;
using Stock.Infrastructure.BackgroundWorkers;
using Stock.Infrastructure.Cron;
using Stock.Infrastructure.Messaging.Workers;

namespace Stock.Infrastructure.Messaging;

public static class MessagingBuilder
{
    public static void AddMessaging(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(typeof(VersionsBuffer<>));

        builder.Services.AddHostedService<OutboxDispatcherService>();

        builder.Services.AddScoped<IJobEventProcessor<DailyReadModelRequested>, DailyReadModelWorker>();
        builder.Services.AddScoped<DailyCronWorker>();
    }
}
