using Messaging.Abstractions;
using Messaging.Dapper.Migrations;
using Messaging.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.Dapper.Outbox;

public static class RegisterOutbox
{
    public static IMessagingBuilder AddOutbox(this IMessagingBuilder builder,
        Action<OutboxRelayOptions>? configureOptions = null)
    {
        var services = builder.Services;

        services.AddScoped<OutboxWriter>();
        services.AddScoped<OutboxReader>();
        services.AddOutboxRelay<OutboxStore>(configureOptions);
        services.AddScoped<OutboxCleaner>();
        services.AddCleanup();
        services.AddScoped<IPublishTerminal, OutboxPublishTerminal>();
        services.AddSingleton(MessagingMigrations.Outbox);

        return builder;
    }
}