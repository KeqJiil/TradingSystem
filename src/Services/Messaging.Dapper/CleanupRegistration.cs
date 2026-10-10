using Microsoft.Extensions.DependencyInjection;

namespace Messaging.Dapper;

public static class CleanupRegistration
{
    public static IServiceCollection ConfigureCleanup(this IServiceCollection services,
        Action<CleansionOptions> configureOptions)
    {
        var options = new CleansionOptions();
        configureOptions(options);
        options.Validate();

        services.AddOptions<CleansionOptions>().Configure(o =>
        {
            o.OutboxBatchSize = options.OutboxBatchSize;
            o.InboxBatchSize = options.InboxBatchSize;
            o.InboxRetentionMinutes = options.InboxRetentionMinutes;
            o.OutboxRetentionMinutes = options.OutboxRetentionMinutes;
        });

        return services;
    }

    internal static void AddCleanup(this IServiceCollection services)
    {
        services.AddOptions<CleansionOptions>();
        services.AddHostedService<DbCleanUp>();
    }
}
