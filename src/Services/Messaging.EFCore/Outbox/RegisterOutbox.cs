using Messaging.Abstractions;
using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.EFCore.Outbox;

public static class RegisterOutbox
{
    public static IMessagingBuilder AddOutbox<TContext>(this IMessagingBuilder services, Action<OutboxRelayOptions>? configureOptions = null) where TContext : DbContext
    {
        services.Services.AddScoped<OutboxWriter<TContext>>();
        services.Services.AddOutboxRelay<OutboxStore<TContext>>(configureOptions);
        services.Services.AddScoped<OutboxReader<TContext>>();
        services.Services.AddScoped<OutboxCleanser<TContext>>();
        services.Services.AddCleanup<TContext>();
        services.Services.AddScoped<IPublishTerminal, OutboxPublishTerminal<TContext>>();
        return services;
    }
}