namespace Order.Infrastructure.Messaging.Serialization;

public interface IProtobufParsable<TSelf> where TSelf : IProtobufParsable<TSelf>
{
    static abstract TSelf Parse(byte[] bytes);
}
