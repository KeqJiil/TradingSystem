using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.Options;
using Stock.Application.Abstractions;
using Stock.Application.Events;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Consumers.Dlq;

public abstract class DlqRetryableConsumer<TMessage, TCommand>(
    IOptions<DeadLetterOptions> dlqOptions,
    ILogger<DlqRetryableConsumer<TMessage, TCommand>> logger,
    IServiceScopeFactory serviceScopeFactory,
    IDlqEventToCommandMapper<TMessage, TCommand> dlqEventToCommandMapper,
    IKafkaConsumerFactory consumerFactory,
    IDeadLetterPublisher dlq) : BackgroundService where TMessage : BasicEvent where TCommand : IRequest
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

            var attempt = GetAttempt(result.Message.Headers) + 1;
            var ok = await HandleMessageAsync(data, ct);

            if (!ok && attempt < dlqOptions.Value.MaxRetryAttempts)
                await dlq.PublishAsync(SourceTopic, data, true, attempt, ct);
            else if (!ok)
                await dlq.PublishAsync(SourceTopic, data, false, attempt, ct);

            _consumer.Commit(result);
        }
    }

    private async Task<bool> HandleMessageAsync(TMessage message, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        try
        {
            var command = dlqEventToCommandMapper.Map(message);
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