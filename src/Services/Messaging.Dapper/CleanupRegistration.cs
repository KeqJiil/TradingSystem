using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Messaging.Dapper;

public static class CleanupRegistration
{
    public static IServiceCollection ConfigureCleanup(this IServiceCollection services,
        Action<CleansionOptions> configureOptions)
    {
        var options = new CleansionOptions();
        configureOptions(options);
        options.Validate();

        services.AddOptions<CleansionOptions>().Configure(configureOptions);

        return services;
    }

    internal static void AddCleanup(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<CleansionOptions>()
            .Validate<IServiceProviderIsService>(
                (_, registered) => registered.IsService(typeof(ITransactionContext)),
                "Inbox and outbox require a unit of work. Call services.AddUnitOfWork(...).")
            .ValidateOnStart();
        services.AddHostedService<DbCleanUp>();
    }
}
