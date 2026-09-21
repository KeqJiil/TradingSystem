using Confluent.Kafka;
using Microsoft.Data.SqlClient;
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
    IDeadLetterPublisher dlq) : KafkaBackgroundConsumer<PriceChangeRequestedEvent>(consumerFactory, logger)
{
    protected override string GroupId => "price-change-requested-events-group";

    protected override string ClientId => "price-change-requested-events-consumer";

    protected override string Topic => TopicNames.PriceChangeRequested;

    protected override async Task<bool> HandleAsync(PriceChangeRequestedEvent message, Headers headers,
        CancellationToken ct)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var eventStore = scope.ServiceProvider.GetRequiredService<EventStoreService>();
        var mappedEvent = PriceChangeRequestedEventMapper.MapFrom(message);

        try
        {
            await resiliencePipeline.ExecuteAsync(
                async (es, cancellationToken) => { await es.ChangePriceAppendAsync(mappedEvent, cancellationToken); },
                eventStore, ct);
        }
        catch (SqlException ex) when (ex.Number == 2627)
        {
            logger.LogInformation(
                "Price change event {EventId} for aggregate {AggregateId} is already stored, skipping",
                mappedEvent.EventId, message.AggregateId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error occurred while processing price change requested event for aggregate {AggregateId}",
                message.AggregateId);
            try
            {
                await dlq.PublishAsync(TopicNames.PriceChangeRequested, message, ex, 1, ct);
            }
            catch (Exception dlqEx) when (!ct.IsCancellationRequested)
            {
                logger.LogError(dlqEx,
                    "Failed to publish price change requested event for aggregate {AggregateId} to DLQ",
                    message.AggregateId);
                return false;
            }
        }

        return true;
    }
}