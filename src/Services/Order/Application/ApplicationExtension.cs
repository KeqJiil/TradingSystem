using Order.Infrastructure.MediatrPipelines;

namespace Order.Application;

public static class ApplicationExtension
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ApplicationExtension).Assembly);
            cfg.AddOpenBehavior(typeof(TracingBehaviour<,>));
        });

        return services;
    }
}
