using Messaging;
using Messaging.EFCore.Outbox;
using Messaging.Middlewares;
using Order.Infrastructure.Messaging.Translators;
using Order.Infrastructure.Persistence.Configuration;

namespace Order.Infrastructure.Messaging;

public static class MessagingExtension
{
    public static IServiceCollection MapMessaging(this IServiceCollection services)
    {
        services.AddSingleton<IDomainEventTranslator, DomainEventTranslator>();
        services.AddMessaging(cfg =>
        {
            cfg.AddOutbox<OrderDbContext>();

            cfg.AddExceptionHandling();
            cfg.AddTracing();

        }, options => { });

        return services;
    }
}