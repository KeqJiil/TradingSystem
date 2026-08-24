using System.Text.Json;
using Confluent.Kafka;
using Stock.Application.Events;
using Stock.Infrastructure.Persistence;

namespace Stock.Infrastructure.Messaging.Workers;

public class OutboxDispatcherService(
    ILogger<OutboxDispatcherService> logger,
    IOutboxReader reader,
    IOutboxMarker marker,
    IProducer<string, PriceChangedEvent> priceProducer,
    IProducer<string, StockToggledStatusEvent> statusProducer,
    IProducer<string, StockCreatedEvent> stockCreatedProducer) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        do
        {
            var outboxes = await reader.GetPendingAsync(50, 10, stoppingToken);

            var tasks = outboxes.Select(o => ProcessAsync(o, stoppingToken)).ToList();
            var result = await Task.WhenAll(tasks);
            var ids = result.Where(x => x.Item1).Select(x => x.Item2).ToList();

            await marker.MarkCompletedAsync(ids, stoppingToken);
        } 
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task<(bool, Guid)> ProcessAsync(OutboxData data, CancellationToken ct)
    {
        try
        {
            var @event = DispatchOutbox(data.EventType, data.Payload);
            if (@event is null) return (false, data.Id);

            var result = await DispatchProducer(@event, ct);
            return (result, data.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "An unexpected error occurred while processing outbox event {OutboxId}", data.Id);
            return (false, data.Id);
        }
    }

    private async Task<bool> DispatchProducer(StockEvent @event, CancellationToken ct)
    {
        switch (@event)
        {
            case PriceChangedEvent e:
                await priceProducer.ProduceAsync("stock.events", new Message<string, PriceChangedEvent> { Value = e, Key = @event.AggregateId.ToString() }, ct);
                return true;
            case StockCreatedEvent e:
                await stockCreatedProducer.ProduceAsync("stock.events", new Message<string, StockCreatedEvent> { Value = e, Key = @event.AggregateId.ToString() }, ct);
                return true;
            case StockToggledStatusEvent e:
                await statusProducer.ProduceAsync("stock.events", new Message<string, StockToggledStatusEvent> { Value = e, Key = @event.AggregateId.ToString() }, ct);
                return true;
            default:
                return false;
        }
    }
    
    private StockEvent? DispatchOutbox(string eventType, string payload)
    {
        return eventType switch
        {
            EventTypeNames.PriceChanged => JsonSerializer.Deserialize<PriceChangedEvent>(payload),
            EventTypeNames.StockCreated => JsonSerializer.Deserialize<StockCreatedEvent>(payload),
            EventTypeNames.StockToggledStatus => JsonSerializer.Deserialize<StockToggledStatusEvent>(payload),
            _ => null
        };
    }
}

public static class EventTypeNames
{
    public const string PriceChanged = nameof(PriceChangedEvent);
    public const string StockCreated = nameof(StockCreatedEvent);
    public const string StockToggledStatus = nameof(StockToggledStatusEvent);
}