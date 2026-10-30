using Messaging;
using Order.Infrastructure.Messaging.Translators;

namespace Order.Infrastructure.Messaging;

public static class MessagingExtension
{
    public static IServiceCollection MapMessaging(this IServiceCollection services)
    {
        services.AddSingleton<IDomainEventTranslator, DomainEventTranslator>();
        services.AddMessaging(cfg => {}, options => {});

        return services;
    }
}