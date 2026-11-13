using Google.Protobuf;
using Messaging.Abstractions;

namespace Stock.Infrastructure.Serialization;

public sealed class ExternalEventSerializer<TEvent, TExternal>(
    Func<TEvent, TExternal> toExternal,
    Func<TExternal, TEvent> fromExternal) : IMessageSerializer
    where TExternal : IMessage<TExternal>, new()
{
    private static readonly MessageParser<TExternal> Parser = new(() => new TExternal());

    public SerializationResult<byte[]> Serialize<TMessage>(TMessage message)
    {
        if (message is not TEvent @event)
            return new SerializationResult<byte[]>([], false,
                $"{typeof(TMessage).Name} is not {typeof(TEvent).Name}.");

        try
        {
            return new SerializationResult<byte[]>(toExternal(@event).ToByteArray(), true);
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
            return fromExternal(Parser.ParseFrom(message)) is TMessage result
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
