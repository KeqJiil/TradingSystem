using Messaging.Abstractions;
using Messaging.Dapper.Outbox;
using Messaging.Dapper.Transactions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Messaging.Dapper.Tests.UnitTests;

public class RegistrationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddOutbox_AnyRegistrationOrder_TerminalIsOutboxPublishTerminal(bool unitOfWorkFirst)
    {
        await using var provider = Build(b => b.AddOutbox(), unitOfWorkFirst);
        await using var scope = provider.CreateAsyncScope();

        Assert.IsType<OutboxPublishTerminal>(scope.ServiceProvider.GetRequiredService<IPublishTerminal>());
    }

    [Fact]
    public async Task AddOutbox_CalledBeforeOtherBuilderCalls_TerminalIsOutboxPublishTerminal()
    {
        await using var provider = Build(b =>
        {
            b.AddOutbox();
            b.AddMessage<string>("topic", new Serializers.JsonDefaultSerializer());
        });
        await using var scope = provider.CreateAsyncScope();

        Assert.IsType<OutboxPublishTerminal>(scope.ServiceProvider.GetRequiredService<IPublishTerminal>());
    }

    [Fact]
    public async Task AddMessaging_WithoutOutbox_TerminalIsNotOutboxPublishTerminal()
    {
        await using var provider = Build(_ => { });
        await using var scope = provider.CreateAsyncScope();

        Assert.IsNotType<OutboxPublishTerminal>(scope.ServiceProvider.GetRequiredService<IPublishTerminal>());
    }

    [Fact]
    public void AddOutbox_InvalidOptions_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Build(b => b.AddOutbox(o => o.BatchSize = 0)));
    }

    [Fact]
    public async Task AddOutbox_LeaseNotGreaterThanMessageTimeout_RelayFailsToStart()
    {
        await using var provider = Build(b => b.AddOutbox(o => o.LeaseMinutes = 1),
            configureOptions: o => o.MessageTimeoutMs = 60_000);

        Assert.Throws<InvalidOperationException>(() => provider.GetServices<IHostedService>().ToList());
    }

    [Fact]
    public async Task AddOutbox_LeaseGreaterThanMessageTimeout_RelayIsCreated()
    {
        await using var provider = Build(b => b.AddOutbox(o => o.LeaseMinutes = 2),
            configureOptions: o => o.MessageTimeoutMs = 60_000);

        Assert.Contains(provider.GetServices<IHostedService>(), s => s is OutboxDispatchService);
    }
    
    private static ServiceProvider Build(Action<IMessagingBuilder> configure, bool unitOfWorkFirst = true,
        Action<MessagingOptions>? configureOptions = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        if (unitOfWorkFirst) services.AddUnitOfWork(_ => new SqlConnection());
        services.AddMessaging(configure, o =>
        {
            o.BootstrapServers = "localhost:9092";
            configureOptions?.Invoke(o);
        });
        if (!unitOfWorkFirst) services.AddUnitOfWork(_ => new SqlConnection());

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
