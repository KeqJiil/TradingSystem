using Confluent.Kafka;
using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Commands.ToggleStatusReadModel;
using Stock.Application.Events;
using Stock.Presentation.Options;

namespace Stock.Infrastructure.Messaging.Workers;

public class StockToggleStatusEventConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<StockToggleStatusEventConsumer> logger,
    IDeadLetterPublisher dlq) : BackgroundService
{
    private readonly IConsumer<string, StockToggledStatusEvent> _consumer =
        consumerFactory.Create<StockToggledStatusEvent>("stock-toggle-events-group", TopicNames.Stock,
            "stock-toggle-events-consumer");


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

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            try
            {
                await mediator.Send(new ToggleStatusReadModelCommand(data.AggregateId), ct);
            }
            catch (Exception ex)
            {
                await dlq.PublishAsync(TopicNames.Stock, data, ex, ct);
                logger.LogWarning(ex, "Error processing StockToggledStatusEvent");
            }

            _consumer.Commit();
        }
    }
}