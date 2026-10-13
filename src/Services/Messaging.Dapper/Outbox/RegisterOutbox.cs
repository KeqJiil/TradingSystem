using Messaging.Abstractions;
using Messaging.Dapper.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.Dapper.Outbox;

public static class RegisterOutbox
{
    public static void AddOutbox(this IMessagingBuilder builder, Action<OutboxRelayOptions>? configureOptions = null)
    {
        var services = builder.Services;
        var options = new OutboxRelayOptions();
        configureOptions?.Invoke(options);
        options.Validate();

        services.AddOptions<OutboxRelayOptions>().Configure(o => configureOptions?.Invoke(o));

        services.AddScoped<OutboxWriter>();
        services.AddScoped<OutboxReader>();
        services.AddScoped<OutboxCleaner>();
        services.AddCleanup();
        services.AddHostedService<OutboxDispatchService>();
        services.AddScoped<IPublishTerminal, OutboxPublishTerminal>();
        services.AddSingleton(EmbeddedMigration.Read("002_Outbox.sql"));
    }
}
