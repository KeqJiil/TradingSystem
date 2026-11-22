using Messaging.Abstractions;

namespace Order.Infrastructure.Messaging.Serialization;

public sealed class ProtobufParsableSerializer<T> : IMessageSerializer where T : IProtobufParsable<T>
{
    public SerializationResult<byte[]> Serialize<TResult>(TResult message)
    {
        return new SerializationResult<byte[]>([], false, $"{typeof(T).Name} cannot be serialized.");
    }

    public SerializationResult<TResult> Deserialize<TResult>(byte[] message)
    {
        try
        {
            return T.Parse(message) is TResult result
                ? new SerializationResult<TResult>(result, true)
                : new SerializationResult<TResult>(default!, false,
                    $"{typeof(T).Name} is not {typeof(TResult).Name}.");
        }
        catch (Exception ex)
        {
            return new SerializationResult<TResult>(default!, false, ex.Message);
        }
    }
}
