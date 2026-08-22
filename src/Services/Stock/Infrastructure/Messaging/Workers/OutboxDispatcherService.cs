using Confluent.Kafka;
using Stock.Application.Events;
using Stock.Infrastructure.Persistence;

namespace Stock.Infrastructure.Messaging.Workers;

public class OutboxDispatcherService(
    IDbContext dbContext,
    IProducer<string, PriceChangedEvent> priceProducer,
    IProducer<string, StockToggledStatusEvent> statusProducer,
    IProducer<string, StockCreatedEvent> stockCreatedProducer) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        throw new NotImplementedException();
    }
}