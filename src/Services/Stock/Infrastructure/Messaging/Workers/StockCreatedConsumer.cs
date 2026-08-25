using Confluent.Kafka;
using MediatR;
using Stock.Application.Commands.CreateReadModel;
using Stock.Application.Events;

namespace Stock.Infrastructure.Messaging.Workers;

public class StockCreatedConsumer(IKafkaConsumerFactory consumerFactory, IMediator mediator) : BackgroundService
{
    private readonly IConsumer<string, StockCreatedEvent> _consumer =
        consumerFactory.Create<StockCreatedEvent>(groupId: "stock-created-events-group", clientId: "stock-created-events-consumer");
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Factory.StartNew(
            () => Consume(stoppingToken),
            stoppingToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private async Task Consume(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var data = _consumer.Consume(ct).Message.Value;
            if (data is null) return;

            var command = new CreateReadModelCommand(data.AggregateId, data.Name, data.IsOpenToTrade, data.Currency,
                data.TradingStartTime, data.TradingCloseTime);

            await mediator.Send(command, ct);
        }
    }
}