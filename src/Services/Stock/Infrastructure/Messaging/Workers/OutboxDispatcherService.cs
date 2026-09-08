using System.Text.Json;
using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Events;
using Stock.Infrastructure.Persistence;
using Stock.Presentation.Options;

namespace Stock.Infrastructure.Messaging.Workers;

public class OutboxDispatcherService(
    ILogger<OutboxDispatcherService> logger,
    IServiceScopeFactory scopeFactory,
    IDeadLetterPublisher dlq) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        do
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var reader = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
            var marker = scope.ServiceProvider.GetRequiredService<IOutboxMarker>();


            var outboxes = await reader.GetPendingAsync(50, 10, stoppingToken);

            var semaphore = new SemaphoreSlim(10);

            var tasks = outboxes.Select(async (o) =>
            {
                await semaphore.WaitAsync(stoppingToken);

                try
                {
                    return await ProcessAsync(o, stoppingToken);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            var result = await Task.WhenAll(tasks);
            var ids = result.Where(x => x.Item1).Select(x => x.Item2).ToList();

            await marker.MarkCompletedAsync(ids, stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task<(bool, Guid)> ProcessAsync(OutboxData data, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        try
        {
            var @event = DispatchOutbox(data.EventType, data.Payload);
            if (@event is null)
            {
                await dlq.PublishUnknownAsync(data, ct);
                return (true, data.Id);
            }

            ;

            await mediator.Publish(@event, ct);
            return (true, data.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "An unexpected error occurred while processing outbox event {OutboxId}", data.Id);
            return (false, data.Id);
        }
    }

    private BasicEvent? DispatchOutbox(string eventType, string payload)
    {
        return eventType switch
        {
            EventTypeNames.PriceChanged => JsonSerializer.Deserialize<PriceChangedEvent>(payload),
            EventTypeNames.StockCreated => JsonSerializer.Deserialize<StockCreatedEvent>(payload),
            EventTypeNames.StockToggledStatus => JsonSerializer.Deserialize<StockToggledStatusEvent>(payload),
            EventTypeNames.DailyReadModelRequested => JsonSerializer.Deserialize<DailyReadModelRequested>(payload),
            _ => null
        };
    }
}

public static class EventTypeNames
{
    public const string PriceChanged = nameof(PriceChangedEvent);
    public const string StockCreated = nameof(StockCreatedEvent);
    public const string StockToggledStatus = nameof(StockToggledStatusEvent);
    public const string DailyReadModelRequested = nameof(DailyReadModelRequested);
}