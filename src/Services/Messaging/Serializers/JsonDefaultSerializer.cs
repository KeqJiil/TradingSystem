using System.Text.Json;
using Messaging.Abstractions;

namespace Messaging.Serializers;

public class JsonDefaultSerializer : IMessageSerializer
{
    public SerializationResult<byte[]> Serialize<TMessage>(TMessage message)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(message);
            return new SerializationResult<byte[]>(bytes, true);
        }
        catch (Exception ex)
        {
            return new SerializationResult<byte[]>(Array.Empty<byte>(), false, ex.Message);
        }
    }

    public SerializationResult<TMessage> Deserialize<TMessage>(byte[] message)
    {
        try
        {
            var result = JsonSerializer.Deserialize<TMessage>(message);
            return new SerializationResult<TMessage>(result!, true);
        }
        catch (Exception ex)
        {
            return new SerializationResult<TMessage>(default!, false, ex.Message);
        }
    }
}