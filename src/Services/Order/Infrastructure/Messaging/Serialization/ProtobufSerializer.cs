using Google.Protobuf;
using Messaging.Abstractions;

namespace Order.Infrastructure.Messaging.Serialization;

public sealed class ProtobufSerializer<T> : IMessageSerializer where T : IMessage<T>, new()
{
    private static readonly MessageParser<T> Parser = new(() => new T());

    public SerializationResult<byte[]> Serialize<TMessage>(TMessage message)
    {
        return message is T protoMessage
            ? new SerializationResult<byte[]>(protoMessage.ToByteArray(), true)
            : new SerializationResult<byte[]>([], false, $"{typeof(TMessage).Name} is not {typeof(T).Name}.");
    }

    public SerializationResult<TMessage> Deserialize<TMessage>(byte[] message)
    {
        try
        {
            return Parser.ParseFrom(message) is TMessage result
                ? new SerializationResult<TMessage>(result, true)
                : new SerializationResult<TMessage>(default!, false, $"{typeof(T).Name} is not {typeof(TMessage).Name}.");
        }
        catch (InvalidProtocolBufferException ex)
        {
            return new SerializationResult<TMessage>(default!, false, ex.Message);
        }
    }
}
