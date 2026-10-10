using Messaging.Dapper.DbTasks;
using Messaging.Dapper.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.Dapper.Inbox;

public static class RegisterInbox
{
    public static IMessagingBuilder AddInbox(this IMessagingBuilder builder)
    {
        builder.Services.AddScoped<DbTaskQueue>();
        builder.Services.AddScoped<IDbTaskQueue>(sp => sp.GetRequiredService<DbTaskQueue>());
        builder.Services.AddScoped<ITransactionHook>(sp => sp.GetRequiredService<DbTaskQueue>());
        builder.Services.AddScoped<InboxWriter>();
        builder.Services.AddSingleton(EmbeddedMigration.Read("001_Inbox.sql"));
        return builder.AddDeliveryMiddleware<InboxMiddleware>();
    }
}
