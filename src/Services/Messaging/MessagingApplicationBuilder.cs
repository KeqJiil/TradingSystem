using Microsoft.Extensions.DependencyInjection;

namespace Messaging;

public static class MessagingApplicationBuilder
{
    public static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        return services;
    }
}