using Messaging.Abstractions;
using Messaging.Dapper.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Messaging.Dapper.Outbox;

public static class RegisterOutbox
{
    public static void AddOutbox(this IServiceCollection services)
    {
        services.AddScoped<OutboxWriter>();
        services.AddScoped<OutboxReader>();
        services.RemoveAll<IPublishTerminal>();
        services.AddScoped<IPublishTerminal, OutboxPublishTerminal>();
        services.AddSingleton(EmbeddedMigration.Read("002_Outbox.sql"));
    }
}
