using Microsoft.Extensions.DependencyInjection;

namespace Messaging.Outbox;

internal static class RegisterOutboxRelay
{
    public static IServiceCollection AddOutboxRelay<TStore>(this IServiceCollection services,
        Action<OutboxRelayOptions>? configureOptions) where TStore : class, IOutboxStore
    {
        var options = new OutboxRelayOptions();
        configureOptions?.Invoke(options);
        options.Validate();

        services.AddOptions<OutboxRelayOptions>().Configure(o => configureOptions?.Invoke(o));
        services.AddScoped<IOutboxStore, TStore>();
        services.AddHostedService<OutboxDispatchService>();
        if (options.AgeMetricInterval is not null) services.AddHostedService<OutboxAgeSampler>();

        return services;
    }
}
