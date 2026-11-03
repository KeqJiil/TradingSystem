using Messaging.Abstractions;
using Messaging.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Order.Application;
using Order.Infrastructure.Messaging;
using Order.Infrastructure.Persistence;

namespace Order.Tests.Infrastructure;

public static class OrderServices
{
    public static ServiceProvider Build(string connectionString, Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OrderDbConnectionString"] = connectionString,
                ["KafkaOptions:BootstrapServers"] = "localhost:9092",
                ["KafkaOptions:ProducerClientId"] = "order-tests-producer"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddPersistence(configuration);
        services.MapMessaging(configuration);
        services.AddSingleton(sp =>
            new PublishedMessages(connectionString, sp.GetRequiredService<IMessageSerializerResolver>()));
        configure?.Invoke(services);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
