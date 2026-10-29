using Messaging;

namespace Order.Infrastructure.Messaging;

public static class MessagingExtension
{
    public static IServiceCollection MapMessaging(this IServiceCollection services)
    {
        services.AddMessaging(cfg => {}, options => {});

        return services;
    }
}