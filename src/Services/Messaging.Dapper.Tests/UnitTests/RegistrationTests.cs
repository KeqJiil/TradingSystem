using Messaging.Abstractions;
using Messaging.Dapper.Inbox;
using Messaging.Dapper.Outbox;
using Messaging.Dapper.Transactions;
using Messaging.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;

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
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InboxOrOutbox_WithoutUnitOfWork_FailsStartupValidation(bool outbox)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMessaging(b =>
        {
            if (outbox) b.AddOutbox();
            else b.AddInbox();
        }, o => o.BootstrapServers = "localhost:9092");
        await using var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains("AddUnitOfWork", ex.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddOutbox_WithUnitOfWorkInAnyOrder_PassesStartupValidation(bool unitOfWorkFirst)
    {
        await using var provider = Build(b => b.AddOutbox(), unitOfWorkFirst);

        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Fact]
    public async Task AddUnitOfWork_AppRegistersOwnResiliencePipeline_PipelinesStaySeparate()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(ResiliencePipeline.Empty);
        services.AddUnitOfWork(_ => new SqlConnection());
        await using var provider = services.BuildServiceProvider();

        Assert.Same(ResiliencePipeline.Empty, provider.GetRequiredService<ResiliencePipeline>());
        Assert.NotSame(ResiliencePipeline.Empty,
            provider.GetRequiredKeyedService<ResiliencePipeline>(UnitOfWorkDecorator.PipelineKey));
    }

    [Fact]
    public void AddUnitOfWork_SyncScopeDisposal_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUnitOfWork(_ => new SqlConnection());
        using var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
            scope.ServiceProvider.GetRequiredService<IUnitOfWorkDecorator>();
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
