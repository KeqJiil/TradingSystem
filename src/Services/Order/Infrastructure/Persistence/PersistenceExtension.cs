using Microsoft.EntityFrameworkCore;
using Order.Application.Abstractions;
using Order.Infrastructure.Persistence.Configuration;
using Order.Infrastructure.Persistence.Implementations;
using Order.Infrastructure.Persistence.Interceptors;

namespace Order.Infrastructure.Persistence;

public static class PersistenceExtension
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<MessagePublishingInterceptor>();

        services.AddDbContext<OrderDbContext>((sp, cfg) =>
            cfg.UseSqlServer(configuration.GetConnectionString("OrderDbConnectionString"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(OrderDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
            }).AddInterceptors(sp.GetRequiredService<MessagePublishingInterceptor>()));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOrderReader, OrderReader>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        return services;
    }
}
