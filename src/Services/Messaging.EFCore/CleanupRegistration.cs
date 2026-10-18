using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Messaging.EFCore;

public static class CleanupRegistration
{
    public static IServiceCollection ConfigureCleanup(this IServiceCollection services,
        Action<CleanupOptions> configureOptions)
    {
        var options = new CleanupOptions();
        configureOptions(options);
        options.Validate();

        services.AddOptions<CleanupOptions>().Configure(configureOptions);

        return services;
    }

    internal static void AddCleanup<TContext>(this IServiceCollection services) where TContext : DbContext
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<CleanupOptions>();
        services.AddHostedService<CleanupService<TContext>>();
    }
}
