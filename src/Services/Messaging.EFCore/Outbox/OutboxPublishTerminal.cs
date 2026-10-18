using System.Text.Json;
using Messaging.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Messaging.EFCore.Outbox;

internal class OutboxPublishTerminal<TContext>(IMessageSerializerResolver serializers, OutboxWriter<TContext> writer)
    : IPublishTerminal where TContext : DbContext
{
    public async Task<PublishOutcome> SendAsync<TMessage>(string topic, string key, string messageId, TMessage payload,
        IDictionary<string, string> headers,
        CancellationToken ct)
    {
        var serialized = serializers.For<TMessage>().Serialize(payload);
        if (!serialized.Success) return new PublishOutcome(false, serialized.Error ?? "Failed to serialize payload");

        await writer.WriteAsync(new OutboxEntity
        {
            MessageId = messageId,
            MessageType = typeof(TMessage).Name,
            Topic = topic,
            MessageKey = key,
            Payload = serialized.Message,
            Headers = JsonSerializer.Serialize(headers)
        }, ct);

        return new PublishOutcome(true);
    }
}