using System.Diagnostics;
using MediatR;
using Stock.Application.Events;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Observability;
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
        CorrelationContext.CorrelationId = data.CorrelationId;

        var known = OutboxEventRegistry.TryGet(data.EventType, out var descriptor);
        using var activity = OutboxTelemetry.StartDispatch(data, startNewTrace: known && descriptor.IsJob);

        try
        {
            if (!known)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Unknown event type");
                logger.LogWarning(
                    "Outbox event {OutboxId} of unknown type {EventType} for aggregate {AggregateId}, moving it to unknown DLQ",
                    data.Id, data.EventType, data.AggregateId);
                await dlq.PublishUnknownAsync(data, ct);
                return (true, data.Id);
            }

            var @event = descriptor.Deserialize(data.Payload);
            if (@event is null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Payload can't be deserialized");
                logger.LogWarning(
                    "Outbox event {OutboxId} of type {EventType} for aggregate {AggregateId} can't be deserialized, moving it to unknown DLQ",
                    data.Id, data.EventType, data.AggregateId);
                await dlq.PublishUnknownAsync(data, ct);
                return (true, data.Id);
            }

            if (!OutboxRetryPolicy.ShouldRetry(data.RetryCount))
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Retry attempts exhausted");
                logger.LogWarning(
                    "Outbox event {OutboxId} of type {EventType} for aggregate {AggregateId} exhausted {Attempts} attempts, moving it to DLQ",
                    data.Id, data.EventType, data.AggregateId, data.RetryCount);

                if (!await TryPublishToOwnDeadLetterAsync(descriptor, @event, data, ct))
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
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            logger.LogWarning(ex,
                "An unexpected error occurred while processing outbox event {OutboxId}, type {EventType}, aggregate {AggregateId}, attempts {Attempts}",
                data.Id, data.EventType, data.AggregateId, data.RetryCount);
            return (false, data.Id);
        }
    }

    private async Task<bool> TryPublishToOwnDeadLetterAsync(OutboxEventDescriptor descriptor, BasicEvent @event,
        OutboxData data, CancellationToken ct)
    {
        if (descriptor.PublishToOwnTopic is null) return false;

        try
        {
            await descriptor.PublishToOwnTopic(dlq, @event, data.RetryCount, ct);
            return true;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex,
                "Outbox event {OutboxId} of type {EventType} can't be published to its own DLQ, moving it to unknown DLQ",
                data.Id, data.EventType);
            return false;
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