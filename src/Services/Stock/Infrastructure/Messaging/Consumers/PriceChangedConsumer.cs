using Confluent.Kafka;
using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Commands.ReplayReadModel;
using Stock.Application.Commands.UpdateReadModel;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Consumers;

public class PriceChangedConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<PriceChangedConsumer> logger,
    IDeadLetterPublisher dlq) : KafkaBackgroundConsumer<PriceChangedEvent>(consumerFactory, logger, dlq)
{
    protected override string GroupId => "price-change-events-group";

    protected override string ClientId => "price-change-events-consumer";

    protected override string Topic => TopicNames.Price;

    protected override async Task<bool> HandleAsync(PriceChangedEvent message, Headers headers, CancellationToken ct)
    {
        if (message.Version is not { } version)
        {
            logger.LogWarning("PriceChangedEvent for aggregate {AggregateId} has no version, moving it to fatal DLQ",
                message.AggregateId);
            return await TryPublishToDeadLetterAsync(() =>
                dlq.PublishAsync(TopicNames.Price, message, false, 1, null, ct), message, ct);
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        try
        {
            var outcome = await mediator.Send(
                new UpdateReadModelCommand(message.AggregateId, message.PriceChange, version), ct);

            if (outcome == ReadModelUpdateOutcome.Gap)
                await mediator.Send(new ReplayReadModelCommand(message.AggregateId, version), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error processing PriceChangedEvent with id {AggregateId}", message.AggregateId);

            return await TryPublishToDeadLetterAsync(() =>
                dlq.PublishAsync(TopicNames.Price, message, ex, 1, ct), message, ct);
        }

        return true;
    }

    private async Task<bool> TryPublishToDeadLetterAsync(Func<Task> publish, PriceChangedEvent message,
        CancellationToken ct)
    {
        try
        {
            await publish();
            return true;
        }
        catch (Exception dlqEx) when (!ct.IsCancellationRequested)
        {
            logger.LogError(dlqEx, "Failed to publish price changed event for aggregate {AggregateId} to DLQ",
                message.AggregateId);
            return false;
        }
    }
}
