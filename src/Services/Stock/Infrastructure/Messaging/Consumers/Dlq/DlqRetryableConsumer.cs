using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.Options;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Consumers.Dlq;

public abstract class DlqRetryableConsumer<TMessage, TCommand>(
    IOptions<DeadLetterOptions> dlqOptions,
    ILogger<DlqRetryableConsumer<TMessage, TCommand>> logger,
    IServiceScopeFactory serviceScopeFactory,
    IDlqEventToCommandMapper<TMessage, TCommand> dlqEventToCommandMapper,
    IKafkaConsumerFactory consumerFactory,
    IDeadLetterPublisher dlq) : BackgroundService where TMessage : IExternalEvent where TCommand : IRequest
{
    protected abstract string SourceTopic { get; }
    private IConsumer<string, TMessage> _consumer = null!;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retryTopic = SourceTopic + dlqOptions.Value.TopicSuffix + ".retry";
        _consumer = consumerFactory.Create<TMessage>(
            $"dlq-retryable-{SourceTopic}-group",
            retryTopic,
            $"dlq-retryable-{SourceTopic}-consumer");

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
            var result = _consumer.Consume(ct);
            var data = result.Message.Value;
            if (data is null)
            {
                _consumer.Commit(result);
                continue;
            }

            var command = dlqEventToCommandMapper.Map(data);
            if (command is null)
            {
                logger.LogWarning(
                    "Message of type {MessageType} for aggregate {AggregateId} could not be mapped to a command, moving straight to fatal DLQ",
                    typeof(TMessage).Name, data.AggregateId);
                await dlq.PublishAsync(SourceTopic, data, false, GetAttempt(result.Message.Headers), ct);
                _consumer.Commit(result);
                continue;
            }

            var attempt = GetAttempt(result.Message.Headers) + 1;
            var ok = await HandleMessageAsync(command, ct);

            if (!ok && attempt < dlqOptions.Value.MaxRetryAttempts)
                await dlq.PublishAsync(SourceTopic, data, true, attempt, ct);
            else if (!ok)
                await dlq.PublishAsync(SourceTopic, data, false, attempt, ct);

            _consumer.Commit(result);
        }
    }

    private async Task<bool> HandleMessageAsync(TCommand command, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        try
        {
            await mediator.Send(command, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing message of type {MessageType}. Retrying...", typeof(TMessage).Name);
            return false;
        }
    }

    private static int GetAttempt(Headers headers)
    {
        return headers.TryGetLastBytes("attempt-count", out var bytes) ? BitConverter.ToInt32(bytes) : 0;
    }
}