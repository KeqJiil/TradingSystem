using Confluent.Kafka;
using MediatR;
using Stock.Application.Commands.UpdateTradingTimeReadModel;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Consumers;

public class TimeChangedConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<TimeChangedConsumer> logger,
    IDeadLetterPublisher dlq) : KafkaBackgroundConsumer<TimeChangedEvent>(consumerFactory, logger, dlq)
{
    protected override string GroupId => "time-change-events-group";

    protected override string ClientId => "time-change-events-consumer";

    protected override string Topic => TopicNames.StockTradingTimeChanged;

    protected override async Task<bool> HandleAsync(TimeChangedEvent message, Headers headers, CancellationToken ct)
    {
        var mappedEvent = TimeChangedEventMapper.MapFrom(message);

        var command = new UpdateTradingTimeCommand(mappedEvent.AggregateId,
            mappedEvent.TradingStartTime, mappedEvent.TradingCloseTime, mappedEvent.Version);

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        try
        {
            await mediator.Send(command, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error processing TimeChangedEvent with id {AggregateId}", message.AggregateId);

            try
            {
                await dlq.PublishAsync(TopicNames.StockTradingTimeChanged, message, ex, 1, ct);
            }
            catch (Exception dlqEx) when (!ct.IsCancellationRequested)
            {
                logger.LogError(dlqEx,
                    "Failed to publish stock trading time changed event for aggregate {AggregateId} to DLQ",
                    message.AggregateId);
                return false;
            }
        }

        return true;
    }
}