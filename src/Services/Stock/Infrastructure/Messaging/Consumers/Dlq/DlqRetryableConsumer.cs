using Confluent.Kafka;
using FluentValidation;
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
    IDeadLetterPublisher dlq) : KafkaBackgroundConsumer<TMessage>(consumerFactory, logger, dlq)
    where TMessage : IExternalEvent where TCommand : IRequest
{
    private static readonly TimeSpan _maxWaitPerPoll = TimeSpan.FromSeconds(30);

    protected abstract string SourceTopic { get; }

    protected override string GroupId => $"dlq-retryable-{SourceTopic}-group";

    protected override string ClientId => $"dlq-retryable-{SourceTopic}-consumer";

    protected override string Topic => SourceTopic + dlqOptions.Value.TopicSuffix + ".retry";

    protected override string DeadLetterSourceTopic => SourceTopic;

    protected override async Task<bool> HandleAsync(TMessage message, Headers headers, CancellationToken ct)
    {
        if (!await WaitUntilDueAsync(headers, ct)) return false;

        var command = dlqEventToCommandMapper.Map(message);

        if (command is null)
        {
            logger.LogWarning(
                "Message of type {MessageType} for aggregate {AggregateId} could not be mapped to a command, moving straight to fatal DLQ",
                typeof(TMessage).Name, message.AggregateId);
            try
            {
                await dlq.PublishAsync(SourceTopic, message, false, GetAttempt(headers), null, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex,
                    "Failed to publish message of type {MessageType} for aggregate {AggregateId} to fatal DLQ",
                    typeof(TMessage).Name, message.AggregateId);
                return false;
            }

            return true;
        }

        var attempt = GetAttempt(headers) + 1;
        var error = await HandleMessageAsync(command, ct);

        if (error is null) return true;

        var retryable = attempt < dlqOptions.Value.MaxRetryAttempts && RetryableErrors.IsRetryable(error);

        try
        {
            await dlq.PublishAsync(SourceTopic, message, retryable, attempt, error, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex,
                "Failed to publish message of type {MessageType} for aggregate {AggregateId} to DLQ",
                typeof(TMessage).Name, message.AggregateId);
            return false;
        }

        return true;
    }

    private async Task<Exception?> HandleMessageAsync(TCommand command, CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        try
        {
            await mediator.Send(command, cancellationToken);
            return null;
        }
        catch (ValidationException ex)
        {
            logger.LogWarning(ex,
                "Message of type {MessageType} failed validation, moving straight to fatal DLQ",
                typeof(TMessage).Name);
            return ex;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error processing message of type {MessageType}. Retrying...", typeof(TMessage).Name);
            return ex;
        }
    }

    private async Task<bool> WaitUntilDueAsync(Headers headers, CancellationToken ct)
    {
        if (!headers.TryGetLastBytes("timestamp", out var bytes)) return true;

        var publishedAt = DateTimeOffset.FromUnixTimeMilliseconds(BitConverter.ToInt64(bytes));
        var remaining = publishedAt + dlqOptions.Value.RetryDelay - DateTimeOffset.UtcNow;

        if (remaining <= TimeSpan.Zero) return true;

        if (remaining > _maxWaitPerPoll)
        {
            await Task.Delay(_maxWaitPerPoll, ct);
            return false;
        }

        await Task.Delay(remaining, ct);
        return true;
    }

    private static int GetAttempt(Headers headers)
    {
        return headers.TryGetLastBytes("attempt-count", out var bytes) ? BitConverter.ToInt32(bytes) : 0;
    }
}