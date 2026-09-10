using Confluent.Kafka;
using MediatR;
using Stock.Application.Commands.ToggleStatusReadModel;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Consumers;

public class StockToggleStatusEventConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<StockToggleStatusEventConsumer> logger,
    IDeadLetterPublisher dlq) : BackgroundService
{
    private readonly IConsumer<string, StockToggledStatusEvent> _consumer =
        consumerFactory.Create<StockToggledStatusEvent>("stock-toggle-events-group", TopicNames.StockStatusToggled,
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

            var mappedEvent = StockToggledStatusEventMapper.MapFrom(data);

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            try
            {
                await mediator.Send(new ToggleStatusReadModelCommand(mappedEvent.AggregateId), ct);
            }
            catch (Exception ex)
            {
                await dlq.PublishAsync(TopicNames.StockStatusToggled, data, ex, attempt: 1, ct);
                logger.LogWarning(ex, "Error processing StockToggledStatusEvent");
            }

            _consumer.Commit();
        }
    }
}