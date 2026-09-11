using Confluent.Kafka;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Consumers;

public abstract class Consumer<TEvent>(
    ILogger<Consumer<TEvent>> logger,
    IKafkaConsumerFactory consumerFactory,
    string groupId,
    string clientId,
    string topicName) : BackgroundService
{
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
        var consumer = consumerFactory.Create<TEvent>(groupId, topicName, clientId);

        while (!ct.IsCancellationRequested) await Handle(consumer.Consume(ct).Message.Value, ct);
    }

    protected abstract Task Handle(TEvent message, CancellationToken ct);
}