using Messaging;
using Messaging.Abstractions;
using Messaging.Dapper.Outbox;
using Messaging.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stock.Infrastructure.Persistence;
using Stock.Presentation.Builder;

namespace Stock.Tests.Infrastructure;

public static class StockServices
{
    public static ServiceProvider Build(string connectionString, Action<MessagingBuilder>? configureMessaging = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString
        });

        builder.AddResilence();
        builder.AddPersistence();
        builder.AddApplication();
        builder.Services.AddMessaging(b =>
        {
            b.AddOutbox();
            configureMessaging?.Invoke(b);
        }, o => o.BootstrapServers = "localhost:9092");
        builder.Services.AddSingleton(sp =>
            new PublishedMessages(connectionString, sp.GetRequiredService<IMessageSerializerResolver>()));

        return builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
