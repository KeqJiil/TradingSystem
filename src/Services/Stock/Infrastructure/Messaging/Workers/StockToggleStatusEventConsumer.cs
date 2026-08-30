using Confluent.Kafka;
using MediatR;
using Stock.Application.Commands.ToggleStatusReadModel;
using Stock.Application.Events;

namespace Stock.Infrastructure.Messaging.Workers;

public class StockToggleStatusEventConsumer(IKafkaConsumerFactory consumerFactory, IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    private readonly IConsumer<string, StockToggledStatusEvent> _consumer =
        consumerFactory.Create<StockToggledStatusEvent>(groupId: "stock-toggle-events-group", clientId: "stock-toggle-events-consumer");

    
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

            using var scope = serviceScopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            
            await mediator.Send(new ToggleStatusReadModelCommand(data.AggregateId), ct);
            
            _consumer.Commit();
        }
    }
}