using System.Text.Json;
using Messaging.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Messaging.EFCore.Outbox;

internal class OutboxPublishTerminal<TContext>(IMessageSerializerResolver serializers, OutboxWriter<TContext> writer)
    : IPublishTerminal where TContext : DbContext
{
    public Task<PublishOutcome> SendAsync<TMessage>(string topic, string key, string messageId, TMessage payload,
        IDictionary<string, string> headers,
        CancellationToken ct)
    {
        var serialized = serializers.For<TMessage>().Serialize(payload);
        if (!serialized.Success)
            return Task.FromResult(new PublishOutcome(false, serialized.Error ?? "Failed to serialize payload"));

        writer.Write(new OutboxEntity
        {
            MessageId = messageId,
            MessageType = typeof(TMessage).Name,
            Topic = topic,
            MessageKey = key,
            Payload = serialized.Message,
            Headers = JsonSerializer.Serialize(headers)
        });

        return Task.FromResult(new PublishOutcome(true));
    }
}