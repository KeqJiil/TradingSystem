using Messaging.Abstractions;

namespace Stock.Application.Services;

public static class MessagePublisherExtensions
{
    public static async Task PublishOrThrowAsync<TMessage>(this IMessagePublisher publisher, TMessage message,
        Guid key, CancellationToken cancellationToken)
    {
        var outcome = await publisher.PublishAsync(message, new PublishOptions(key.ToString()), cancellationToken);
        if (!outcome.IsSuccessful)
            throw new InvalidOperationException(
                $"Failed to publish {typeof(TMessage).Name} for {key}: {outcome.ErrorMessage}");
    }
}
