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
            var logger = sp.GetRequiredService<ILogger<VersionsBuffer<MessageEnvelope<PriceChangedEvent>>>>();
            var clock = sp.GetRequiredService<ISystemClock>();
            var dlq = sp.GetRequiredService<IDeadLetterPublisher>();

            async Task OnExpire(Guid aggregateId, IReadOnlyCollection<MessageEnvelope<PriceChangedEvent>> expired,
                CancellationToken ct)
            {
                foreach (var evt in expired)
                {
                    CorrelationContext.CorrelationId = evt.CorrelationId;
                    await dlq.PublishAsync(TopicNames.Price, evt.Message, true, 1, ct);
                }
            }

            return new VersionsBuffer<MessageEnvelope<PriceChangedEvent>>(logger, clock, OnExpire);
        });

        builder.Services.AddHostedService<OutboxDispatcherService>();

        builder.Services.AddScoped<IJobEventProcessor<DailyReadModelRequested>, DailyReadModelWorker>();
        builder.Services.AddScoped<DailyCronWorker>();
        builder.Services.AddScoped<IJobEventProcessor<HourlyReadModelRequested>, HourlyReadModelWorker>();
        builder.Services.AddScoped<HourlyCronWorker>();
        builder.Services.AddScoped<OutboxCleanupCronWorker>();
    }
}