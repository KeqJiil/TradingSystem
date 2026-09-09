using Confluent.Kafka;
using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Commands.UpdateReadModel;
using Stock.Application.Events;
using Stock.Presentation.Options;

namespace Stock.Infrastructure.Messaging.Consumers;

public class PriceChangedConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory serviceScopeFactory,
    VersionsBuffer<PriceChangedEvent> buffer,
    ILogger<PriceChangedConsumer> logger
    ) : BackgroundService
{
    private readonly IConsumer<string, PriceChangedEvent> _consumer =
        consumerFactory.Create<PriceChangedEvent>("price-change-events-group", TopicNames.Price,
            "price-change-events-consumer");

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
            if (data is not { Version: { } version }) continue;

            await buffer.TryApplyAsync(data.AggregateId, version, data, ApplyAsync, ct);

            if (!buffer.HasPendingGaps) _consumer.Commit();
        }
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