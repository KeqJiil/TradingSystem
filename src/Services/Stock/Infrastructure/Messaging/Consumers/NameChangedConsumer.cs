using Confluent.Kafka;
using MediatR;
using Stock.Application.Commands.UpdateNameReadModel;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Consumers;

public class NameChangedConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<NameChangedConsumer> logger,
    IDeadLetterPublisher dlq) : KafkaBackgroundConsumer<NameChangedEvent>(consumerFactory, logger, dlq)
{
    protected override string GroupId => "name-change-events-group";

    protected override string ClientId => "name-change-events-consumer";

    protected override string Topic => TopicNames.StockNameChanged;

    protected override async Task<bool> HandleAsync(NameChangedEvent message, Headers headers, CancellationToken ct)
    {
        var mappedEvent = NameChangedEventMapper.MapFrom(message);

        var command = new UpdateNameReadModelCommand(mappedEvent.AggregateId, mappedEvent.Name, mappedEvent.Version);

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        try
        {
            await mediator.Send(command, ct);
        }
        catch (Exception ex)
        {
            try
            {
                await dlq.PublishAsync(TopicNames.StockNameChanged, message, ex, 1, ct);
            }
            catch (Exception dlqEx) when (!ct.IsCancellationRequested)
            {
                logger.LogError(dlqEx,
                    "Failed to publish stock trading name changed event for aggregate {AggregateId} to DLQ",
                    message.AggregateId);
                return false;
            }

            logger.LogWarning(ex, "Error processing NameChangedEvent");
        }

        return true;
    }
}