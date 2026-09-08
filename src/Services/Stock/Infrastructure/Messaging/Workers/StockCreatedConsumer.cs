using Confluent.Kafka;
using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Commands.CreateReadModel;
using Stock.Application.Events;
using Stock.Presentation.Options;

namespace Stock.Infrastructure.Messaging.Workers;

public class StockCreatedConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<StockCreatedConsumer> logger,
    IDeadLetterPublisher dlq) : BackgroundService
{
    private readonly IConsumer<string, StockCreatedEvent> _consumer =
        consumerFactory.Create<StockCreatedEvent>("stock-created-events-group", TopicNames.Stock,
            "stock-created-events-consumer");

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

            try
            {
                await mediator.Send(command, ct);
            }
            catch (Exception ex)
            {
                await dlq.PublishAsync(TopicNames.Stock, data, ex, ct);
                logger.LogWarning(ex, "Error processing StockCreatedEvent");
            }

            _consumer.Commit();
        }
    }
}