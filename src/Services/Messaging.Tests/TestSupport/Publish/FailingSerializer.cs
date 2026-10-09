namespace Messaging.Tests.TestSupport.Publish;

public sealed class FailingSerializer : IMessageSerializer
{
    public SerializationResult<byte[]> Serialize<TMessage>(TMessage message)
    {
        return new SerializationResult<byte[]>(Array.Empty<byte>(), false, "cannot serialize");
    }

    public SerializationResult<TMessage> Deserialize<TMessage>(byte[] message)
    {
        return new SerializationResult<TMessage>(default!, false, "cannot deserialize");
    }
}
