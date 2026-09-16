using Confluent.Kafka;
using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Commands.UpdateReadModel;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Consumers;

public class PriceChangedConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory serviceScopeFactory,
    VersionsBuffer<PriceChangedEvent> buffer,
    ILogger<PriceChangedConsumer> logger) : KafkaBackgroundConsumer<PriceChangedEvent>(consumerFactory, logger)
{
    protected override string GroupId => "price-change-events-group";

    protected override string ClientId => "price-change-events-consumer";

    protected override string Topic => TopicNames.Price;

    protected override async Task<bool> HandleAsync(PriceChangedEvent message, Headers headers, CancellationToken ct)
    {
        if (message is { Version: { } version })
            await buffer.TryApplyAsync(message.AggregateId, version, message, ApplyAsync, ct);

        return !buffer.HasPendingGaps;
    }

    private async Task<ReadModelUpdateOutcome> ApplyAsync(Guid aggregateId, long version, PriceChangedEvent data,
        CancellationToken ct)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        try
        {
            return await mediator.Send(new UpdateReadModelCommand(aggregateId, data.PriceChange, version), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while applying price change event for aggregate {AggregateId}",
                aggregateId);
            return ReadModelUpdateOutcome.Gap;
        }
    }
}
