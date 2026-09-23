using Confluent.Kafka;
using MediatR;
using Stock.Application.Commands.CreateReadModel;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Consumers;

public class StockCreatedConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<StockCreatedConsumer> logger,
    IDeadLetterPublisher dlq) : KafkaBackgroundConsumer<StockCreatedEvent>(consumerFactory, logger, dlq)
{
    protected override string GroupId => "stock-created-events-group";

    protected override string ClientId => "stock-created-events-consumer";

    protected override string Topic => TopicNames.StockCreated;

    protected override async Task<bool> HandleAsync(StockCreatedEvent message, Headers headers, CancellationToken ct)
    {
        var mappedEvent = StockCreatedEventMapper.MapFrom(message);
        var command = new CreateReadModelCommand(mappedEvent.AggregateId, mappedEvent.Name,
            mappedEvent.IsOpenToTrade, mappedEvent.Currency, mappedEvent.TradingStartTime,
            mappedEvent.TradingCloseTime);

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        try
        {
            await mediator.Send(command, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error processing StockCreatedEvent with id {AggregateId}", message.AggregateId);

            try
            {
                await dlq.PublishAsync(TopicNames.StockCreated, message, ex, 1, ct);
            }
            catch (Exception dlqEx) when (!ct.IsCancellationRequested)
            {
                logger.LogError(dlqEx,
                    "Failed to publish stock created event for aggregate {AggregateId} to DLQ",
                    message.AggregateId);
                return false;
            }
        }

        return true;
    }
}