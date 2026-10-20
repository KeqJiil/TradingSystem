using Messaging.Abstractions;
using ProtoBuf;

namespace Stock.Infrastructure.Serialization;

public sealed class ExternalEventSerializer<TEvent, TExternal>(
    Func<TEvent, TExternal> toExternal,
    Func<TExternal, TEvent> fromExternal) : IMessageSerializer
{
    public SerializationResult<byte[]> Serialize<TMessage>(TMessage message)
    {
        if (message is not TEvent @event)
            return new SerializationResult<byte[]>([], false,
                $"{typeof(TMessage).Name} is not {typeof(TEvent).Name}.");

        try
        {
            using var stream = new MemoryStream();
            Serializer.Serialize(stream, toExternal(@event));
            return new SerializationResult<byte[]>(stream.ToArray(), true);
        }
        catch (Exception ex)
        {
            return new SerializationResult<byte[]>([], false, ex.Message);
        }
    }

    public SerializationResult<TMessage> Deserialize<TMessage>(byte[] message)
    {
        try
        {
            using var stream = new MemoryStream(message);
            return fromExternal(Serializer.Deserialize<TExternal>(stream)) is TMessage result
                ? new SerializationResult<TMessage>(result, true)
                : new SerializationResult<TMessage>(default!, false,
                    $"{typeof(TEvent).Name} is not {typeof(TMessage).Name}.");
        }
        catch (Exception ex)
        {
            return new SerializationResult<TMessage>(default!, false, ex.Message);
        }
    }
}
