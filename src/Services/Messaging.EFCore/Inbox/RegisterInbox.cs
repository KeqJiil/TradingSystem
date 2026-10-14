namespace Messaging.EFCore.Inbox;

public static class RegisterInbox
{
    public static IMessagingBuilder AddInbox(this IMessagingBuilder services)
    {
        return services;
    }
}