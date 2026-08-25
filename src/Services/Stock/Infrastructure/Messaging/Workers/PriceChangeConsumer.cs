using Confluent.Kafka;
using MediatR;
using Stock.Application.Commands.UpdateReadModel;
using Stock.Application.Events;

namespace Stock.Infrastructure.Messaging.Workers;

public class PriceChangeConsumer(IKafkaConsumerFactory consumerFactory, IMediator mediator) : BackgroundService
{
    private readonly IConsumer<string, PriceChangedEvent> _consumer =
        consumerFactory.Create<PriceChangedEvent>(groupId: "price-change-events-group", clientId: "price-change-events-consumer");
    
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
        if (data is not { Version: { } version }) return;
        
        await mediator.Send(new UpdateReadModelCommand(data.AggregateId, data.PriceChange, version), ct);
    }
}