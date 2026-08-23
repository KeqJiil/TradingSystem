using Confluent.Kafka;
using Stock.Application.Events;
using Stock.Infrastructure.Persistence;

namespace Stock.Infrastructure.Messaging.Workers;

public class OutboxDispatcherService(
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
        throw new NotImplementedException();
    }

    private async Task DispatchProducer()
    {
        throw new NotImplementedException();
    }
    
    private async Task DispatchOutbox()
    {
        throw new NotImplementedException();
    }
}