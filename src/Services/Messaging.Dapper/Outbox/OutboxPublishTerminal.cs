using Messaging.Abstractions;

namespace Messaging.Dapper.Outbox;

internal class OutboxPublishTerminal(OutboxWriter writer, ITransactionContext dbContext, IMessageSerializer serializer)
    : IPublishTerminal
{
    public async Task<PublishOutcome> SendAsync<TMessage>(string topic, string key, string messageId,
        TMessage payload, IDictionary<string, string> headers, CancellationToken ct)
    {
        if (dbContext.Transaction is null)
            throw new InvalidOperationException("Outbox publish requires an active transaction.");

        var serialized = serializer.Serialize(payload);
        if (!serialized.Success) return new PublishOutcome(false, serialized.Error ?? "Failed to serialize payload");

        await writer.WriteAsync(
            new OutboxEntry(messageId, typeof(TMessage).Name, topic, key, serialized.Message,
                new Dictionary<string, string>(headers)), ct);

        return new PublishOutcome(true);
    }
}
