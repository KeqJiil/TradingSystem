using Confluent.Kafka;
using MediatR;
using Stock.Application.Commands.CreateReadModel;
using Stock.Application.Events;

namespace Stock.Infrastructure.Messaging.Workers;

public class StockCreatedConsumer(IKafkaConsumerFactory consumerFactory, IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    private readonly IConsumer<string, StockCreatedEvent> _consumer =
        consumerFactory.Create<StockCreatedEvent>("stock-created-events-group", "stock-created-events-consumer");

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return
            Task.Factory.StartNew(() => Consume(stoppingToken), stoppingToken,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private async Task Consume(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var data = _consumer.Consume(ct).Message.Value;
            if (data is null) return;
            
            var command = new CreateReadModelCommand(data.AggregateId, data.Name, data.IsOpenToTrade, data.Currency,
                data.TradingStartTime, data.TradingCloseTime);

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            
            await mediator.Send(command, ct);
            
            _consumer.Commit();
        }
    }
}