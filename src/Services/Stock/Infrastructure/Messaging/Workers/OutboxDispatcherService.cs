using MediatR;
using Stock.Application.Events;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Persistence;

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
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var reader = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
                var marker = scope.ServiceProvider.GetRequiredService<IOutboxMarker>();


                var outboxes = await reader.GetPendingAsync(50, 10, stoppingToken);

                var semaphore = new SemaphoreSlim(10);

                var tasks = outboxes.Select(async o =>
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
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "An unexpected error occurred while dispatching outbox events");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task<(bool, Guid)> ProcessAsync(OutboxData data, CancellationToken ct)
    {
        try
        {
            CorrelationContext.CorrelationId = data.CorrelationId;

            if (!OutboxEventRegistry.TryGet(data.EventType, out var descriptor))
            {
                logger.LogWarning(
                    "Outbox event {OutboxId} of unknown type {EventType} for aggregate {AggregateId}, moving it to unknown DLQ",
                    data.Id, data.EventType, data.AggregateId);
                await dlq.PublishUnknownAsync(data, ct);
                return (true, data.Id);
            }

            var @event = descriptor.Deserialize(data.Payload);
            if (@event is null)
            {
                logger.LogWarning(
                    "Outbox event {OutboxId} of type {EventType} for aggregate {AggregateId} can't be deserialized, moving it to unknown DLQ",
                    data.Id, data.EventType, data.AggregateId);
                await dlq.PublishUnknownAsync(data, ct);
                return (true, data.Id);
            }

            if (!OutboxRetryPolicy.ShouldRetry(data.RetryCount))
            {
                logger.LogWarning(
                    "Outbox event {OutboxId} of type {EventType} for aggregate {AggregateId} exhausted {Attempts} attempts, moving it to DLQ",
                    data.Id, data.EventType, data.AggregateId, data.RetryCount);

                if (descriptor.PublishToOwnTopic is not null)
                    await descriptor.PublishToOwnTopic(dlq, @event, data.RetryCount, ct);
                else
                    await dlq.PublishUnknownAsync(data, ct);

                return (true, data.Id);
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            await mediator.Publish(@event, ct);
            return (true, data.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "An unexpected error occurred while processing outbox event {OutboxId}, type {EventType}, aggregate {AggregateId}, attempts {Attempts}",
                data.Id, data.EventType, data.AggregateId, data.RetryCount);
            return (false, data.Id);
        }
    }
}

public static class EventTypeNames
{
    public const string PriceChanged = nameof(PriceChangedEvent);
    public const string StockCreated = nameof(StockCreatedEvent);
    public const string StockToggledStatus = nameof(StockToggledStatusEvent);
    public const string NameChanged = nameof(NameChangedEvent);
    public const string TimeChanged = nameof(TimeChangedEvent);
    public const string DailyReadModelRequested = nameof(DailyReadModelRequested);
    public const string HourlyReadModelRequested = nameof(HourlyReadModelRequested);
}