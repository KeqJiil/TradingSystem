namespace Messaging.Abstractions;

public interface IMessageSerializerResolver
{
    IMessageSerializer For<TMessage>();
}
