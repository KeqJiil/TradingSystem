using Messaging.Abstractions;
using Messaging.EFCore.Inbox;
using Messaging.EFCore.Outbox;
using Messaging.EFCore.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Messaging.EFCore.Tests.UnitTests;

public class RegistrationTests
{
    [Fact]
    public async Task AddOutbox_Registered_TerminalIsOutboxPublishTerminal()
    {
        await using var provider = Build(b => b.AddOutbox<TestDbContext>());
        await using var scope = provider.CreateAsyncScope();

        Assert.IsType<OutboxPublishTerminal<TestDbContext>>(scope.ServiceProvider.GetRequiredService<IPublishTerminal>());
    }

    [Fact]
    public async Task AddMessaging_WithoutOutbox_TerminalIsNotOutboxPublishTerminal()
    {
        await using var provider = Build(_ => { });
        await using var scope = provider.CreateAsyncScope();

        Assert.IsNotType<OutboxPublishTerminal<TestDbContext>>(
            scope.ServiceProvider.GetRequiredService<IPublishTerminal>());
    }

    [Fact]
    public void AddOutbox_InvalidOptions_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Build(b => b.AddOutbox<TestDbContext>(o => o.BatchSize = 0)));
    }

    [Fact]
    public async Task AddInbox_Registered_InboxHookAttachedToContext()
    {
        await using var provider = Build(b => b.AddInbox<TestDbContext>());
        await using var scope = provider.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var interceptors = context.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()!.Interceptors!;

        Assert.Contains(scope.ServiceProvider.GetRequiredService<InboxHook>(), interceptors);
    }

    [Fact]
    public async Task AddInboxAndOutbox_Registered_SingleCleanupService()
    {
        await using var provider = Build(b => b.AddInbox<TestDbContext>().AddOutbox<TestDbContext>());

        Assert.Single(provider.GetServices<IHostedService>().OfType<CleanupService<TestDbContext>>());
    }

    private static ServiceProvider Build(Action<IMessagingBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestDbContext>(o => o.UseSqlServer("Server=localhost;Database=Test"));
        services.AddMessaging(configure, o => o.BootstrapServers = "localhost:9092");

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
