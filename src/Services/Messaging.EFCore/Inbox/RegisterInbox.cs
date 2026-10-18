using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.EFCore.Inbox;

public static class RegisterInbox
{
    public static IMessagingBuilder AddInbox<TContext>(this IMessagingBuilder services) where TContext : DbContext
    {
        services.Services.AddScoped<InboxState>();
        services.Services.AddScoped<InboxHook>();
        services.Services.AddScoped<InboxCleanser<TContext>>();
        services.Services.AddCleanup<TContext>();
        services.Services.ConfigureDbContext<TContext>((sp, x) =>
            x.AddInterceptors(sp.GetRequiredService<InboxHook>()));
        services.AddDeliveryMiddleware<InboxMiddleware<TContext>>();
        return services;
    }
}