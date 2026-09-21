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
    IDeadLetterPublisher dlq) : KafkaBackgroundConsumer<StockToggledStatusEvent>(consumerFactory, logger)
{
    protected override string GroupId => "stock-toggle-events-group";

    protected override string ClientId => "stock-toggle-events-consumer";

    protected override string Topic => TopicNames.StockStatusToggled;

    protected override async Task<bool> HandleAsync(StockToggledStatusEvent message, Headers headers,
        CancellationToken ct)
    {
        var mappedEvent = StockToggledStatusEventMapper.MapFrom(message);

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        try
        {
            await mediator.Send(new ToggleStatusReadModelCommand(mappedEvent.AggregateId), ct);
        }
        catch (Exception ex)
        {
            try 
            {
                await dlq.PublishAsync(TopicNames.StockStatusToggled, message, ex, 1, ct);
            }
            catch (Exception dlqEx) when (!ct.IsCancellationRequested)
            {
                logger.LogError(dlqEx, "Failed to publish message to dead letter queue for StockToggledStatusEvent");
                return false;
            }
            logger.LogWarning(ex, "Error processing StockToggledStatusEvent");
        }

        return true;
    }
}