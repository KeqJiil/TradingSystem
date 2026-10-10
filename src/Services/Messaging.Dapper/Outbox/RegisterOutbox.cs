using Messaging.Abstractions;
using Messaging.Dapper.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Messaging.Dapper.Outbox;

public static class RegisterOutbox
{
    public static void AddOutbox(this IServiceCollection services, Action<OutboxRelayOptions>? configureOptions = null)
    {
        var options = new OutboxRelayOptions();
        configureOptions?.Invoke(options);
        options.Validate();

        services.AddOptions<OutboxRelayOptions>().Configure(o =>
        {
            o.PollInterval = options.PollInterval;
            o.BatchSize = options.BatchSize;
            o.MaxParallelKeys = options.MaxParallelKeys;
            o.LeaseMinutes = options.LeaseMinutes;
        });

        services.AddScoped<OutboxWriter>();
        services.AddScoped<OutboxReader>();
        services.AddScoped<OutboxCleaner>();
        services.AddCleanup();
        services.AddHostedService<OutboxDispatchService>();
        services.RemoveAll<IPublishTerminal>();
        services.AddScoped<IPublishTerminal, OutboxPublishTerminal>();
        services.AddSingleton(EmbeddedMigration.Read("002_Outbox.sql"));
    }
}
