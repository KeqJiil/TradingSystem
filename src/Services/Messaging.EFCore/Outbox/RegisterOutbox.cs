using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.EFCore.Outbox;

public static class RegisterOutbox
{
    public static IMessagingBuilder AddOutbox<TContext>(this IMessagingBuilder services) where TContext : DbContext
    {
        services.Services.AddDbContext<TContext>();
        return services;
    }
}