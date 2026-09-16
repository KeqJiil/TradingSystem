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
    IDeadLetterPublisher dlq) : KafkaBackgroundConsumer<TMessage>(consumerFactory, logger)
    where TMessage : IExternalEvent where TCommand : IRequest
{
    protected abstract string SourceTopic { get; }

    protected override string GroupId => $"dlq-retryable-{SourceTopic}-group";

    protected override string ClientId => $"dlq-retryable-{SourceTopic}-consumer";

    protected override string Topic => SourceTopic + dlqOptions.Value.TopicSuffix + ".retry";

    protected override async Task<bool> HandleAsync(TMessage message, Headers headers, CancellationToken ct)
    {
        var command = dlqEventToCommandMapper.Map(message);

        if (command is null)
        {
            logger.LogWarning(
                "Message of type {MessageType} for aggregate {AggregateId} could not be mapped to a command, moving straight to fatal DLQ",
                typeof(TMessage).Name, message.AggregateId);
            await dlq.PublishAsync(SourceTopic, message, false, GetAttempt(headers), ct);
            return true;
        }

        var attempt = GetAttempt(headers) + 1;
        var ok = await HandleMessageAsync(command, ct);

        if (!ok)
            await dlq.PublishAsync(SourceTopic, message, attempt < dlqOptions.Value.MaxRetryAttempts, attempt, ct);

        return true;
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
