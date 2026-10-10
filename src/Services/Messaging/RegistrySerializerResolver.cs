using Messaging.Abstractions;

namespace Messaging;

internal class RegistrySerializerResolver(MessagingRegistry registry) : IMessageSerializerResolver
{
    public IMessageSerializer For<TMessage>()
    {
        if (!registry.Messages.TryGetValue(typeof(TMessage), out var message))
            throw new InvalidOperationException(
                $"Message type {typeof(TMessage).Name} is not registered. Call AddMessage<{typeof(TMessage).Name}> first.");

        return message.Serializer;
    }
}
