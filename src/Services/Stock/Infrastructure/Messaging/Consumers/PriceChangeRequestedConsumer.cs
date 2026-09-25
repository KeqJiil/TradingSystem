using Confluent.Kafka;
using MediatR;
using Stock.Application.Commands.ChangePrice;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Consumers;

public class PriceChangeRequestedConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<PriceChangeRequestedConsumer> logger,
    IDeadLetterPublisher dlq) : KafkaBackgroundConsumer<PriceChangeRequestedEvent>(consumerFactory, logger, dlq)
{
    protected override string GroupId => "price-change-requested-events-group";

    protected override string ClientId => "price-change-requested-events-consumer";

    protected override string Topic => TopicNames.PriceChangeRequested;

    protected override async Task<bool> HandleAsync(PriceChangeRequestedEvent message, Headers headers,
        CancellationToken ct)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var mappedEvent = PriceChangeRequestedEventMapper.MapFrom(message);

        try
        {
            await mediator.Send(
                new ChangePriceCommand(mappedEvent.EventId, mappedEvent.AggregateId, mappedEvent.PriceChange,
                    mappedEvent.OccuredAt), ct);
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