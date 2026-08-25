using Confluent.Kafka;
using MediatR;
using Stock.Application.Commands.CreateReadModel;
using Stock.Application.Commands.ToggleStatusReadModel;
using Stock.Application.Events;

namespace Stock.Infrastructure.Messaging.Workers;

public class StockToggleStatusEventConsumer(IKafkaConsumerFactory consumerFactory, IMediator mediator) : BackgroundService
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
        var data = _consumer.Consume(ct).Message.Value;
        if (data is null) return;

        await mediator.Send(new ToggleStatusReadModelCommand(data.AggregateId), ct);
    }
}