using Confluent.Kafka;
using Polly;
using Stock.Application.Services;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Consumers;

public class PriceChangeRequestedConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<PriceChangeRequestedConsumer> logger,
    ResiliencePipeline resiliencePipeline,
    IDeadLetterPublisher dlq
    ) : BackgroundService
{
    private readonly IConsumer<string, PriceChangeRequestedEvent> _consumer =
        consumerFactory.Create<PriceChangeRequestedEvent>("price-change-requested-events-group", TopicNames.PriceChangeRequested,
            "price-change-requested-events-consumer");

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
            if (data is null) continue;

            await ProcessAsync(data.AggregateId, data, ct);

            _consumer.Commit();
        }
    }

    private async Task ProcessAsync(Guid aggregateId, PriceChangeRequestedEvent data,
        CancellationToken ct)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var eventStore = scope.ServiceProvider.GetRequiredService<EventStoreService>();
        var mappedEvent = PriceChangeRequestedEventMapper.MapFrom(data);
        try
        {
            await resiliencePipeline.ExecuteAsync(async (es, cancellationToken) =>
            {
                await es.ChangePriceAppendAsync(mappedEvent, cancellationToken);
            }, eventStore, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while processing price change requested event for aggregate {AggregateId}",
                aggregateId);
            await dlq.PublishAsync(TopicNames.PriceChangeRequested, data, ex, attempt: 1, ct);
        }
    }
}

