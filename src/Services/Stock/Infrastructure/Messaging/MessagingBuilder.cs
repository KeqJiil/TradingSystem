using Microsoft.Extensions.Internal;
using Stock.Application.Abstractions;
using Stock.Application.Events;
using Stock.Infrastructure.BackgroundWorkers;
using Stock.Infrastructure.Cron;
using Stock.Infrastructure.Messaging.Consumers;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Messaging.Workers;
using Stock.Infrastructure.Options;
using PriceChangedEvent = Stock.Infrastructure.ExternalEvents.PriceChangedEvent;

namespace Stock.Infrastructure.Messaging;

public static class MessagingBuilder
{
    public static void AddMessaging(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<VersionsBuffer<PriceChangedEvent>>>();
            var clock = sp.GetRequiredService<ISystemClock>();
            var dlq = sp.GetRequiredService<IDeadLetterPublisher>();

            async Task OnExpire(Guid aggregateId, IReadOnlyCollection<PriceChangedEvent> expired, CancellationToken ct)
            {
                foreach (var evt in expired)
                    await dlq.PublishAsync(TopicNames.Price, evt, isRetryable: true, attempt: 1, ct);
            }

            return new VersionsBuffer<PriceChangedEvent>(logger, clock, OnExpire);
        });

        builder.Services.AddHostedService<OutboxDispatcherService>();

        builder.Services.AddScoped<IJobEventProcessor<DailyReadModelRequested>, DailyReadModelWorker>();
        builder.Services.AddScoped<DailyCronWorker>();
    }
}
