using Messaging;

namespace Messaging.EFCore.Outbox;

public static class RegisterOutbox
{
    public static IMessagingBuilder AddOutbox(this IMessagingBuilder services)
    {
        return services;
    }
}