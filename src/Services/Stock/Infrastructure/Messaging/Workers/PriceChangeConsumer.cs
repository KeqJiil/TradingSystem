using Confluent.Kafka;
using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Commands.UpdateReadModel;
using Stock.Application.Events;

namespace Stock.Infrastructure.Messaging.Workers;

public class PriceChangeConsumer(
    IKafkaConsumerFactory consumerFactory,
    IServiceScopeFactory serviceScopeFactory,
    VersionsBuffer<PriceChangedEvent> buffer) : BackgroundService
{
    private readonly IConsumer<string, PriceChangedEvent> _consumer =
        consumerFactory.Create<PriceChangedEvent>("price-change-events-group", "price-change-events-consumer");

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

            _consumer.Commit();
        }
    }

    private async Task<ReadModelUpdateOutcome> ApplyAsync(Guid aggregateId, long version, PriceChangedEvent data,
        CancellationToken ct)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(new UpdateReadModelCommand(aggregateId, data.PriceChange, version), ct);
    }
}