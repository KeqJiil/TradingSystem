namespace Messaging.Abstractions;

public interface IMessageConsumer<TMessage>
{
    /// <summary>
    /// Consumes a message of type TMessage.
    /// </summary>
    /// <param name="message">The message to consume</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The outcome of the consume operation</returns>
    Task<ConsumeOutcome> ConsumeAsync(TMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Represents the outcome of a message consumption attempt.
/// </summary>
/// <param name="Kind">The result of the message consumption attempt</param>
/// <param name="Reason">The reason for the consumption outcome, if any</param>
public record struct ConsumeOutcome(MessageConsumeResult Kind, string? Reason = null);

/// <summary>
/// Represents the result of a message consumption attempt.
/// Retry: The message consumption failed, and the same message will be redelivered from the current topic (no separate retry topic is used) until attempts are exhausted, after which it is dead-lettered.
/// DeadLetter: The message consumption failed, and the message should be sent to the dead letter topic.
/// Success: The message consumption succeeded, and the offset should be committed.
/// </summary>
public enum MessageConsumeResult
{
    Retry,
    DeadLetter,
    Success
}