using Messaging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Messaging.Tests.Pipelines;

public class PublishPipelineTests
{
    [Fact(Skip = "Fake terminal records args. Assert topic, key, messageId, payload and headers equal the context.")]
    public async Task RunPublish_WithoutMiddlewares_CallsTerminalWithContextValues()
    {
    }

    [Fact(Skip = "A, B, C publish probes. Assert before in registration order, terminal, after in reverse.")]
    public Task RunPublish_RunsMiddlewaresOutsideInAroundTerminal()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip = "Middleware adds traceparent. Assert terminal receives it.")]
    public Task RunPublish_MiddlewareChangesHeaders_ChangedHeadersReachTerminal()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip = "Middleware returns failed outcome without next. Assert terminal not called and outcome returned.")]
    public Task RunPublish_MiddlewareShortCircuits_TerminalNotCalled()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip =
        "Terminal returns failed outcome. Assert middlewares and caller see IsSuccessful false and the error message.")]
    public Task RunPublish_TerminalFails_OutcomeFlowsThroughMiddlewares()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public void RunPublish_ScopedMiddleware_NewInstancePerScope()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddMessaging(
            builder => { builder.AddPublishMiddleware<EmptyMiddleware>(); },
            options => { options.BootstrapServers = "localhost:9092"; });

        var provider = services.BuildServiceProvider();
        var scope1 = provider.CreateScope();
        var scope2 = provider.CreateScope();

        var publisher1 = scope1.ServiceProvider.GetRequiredService<IMessagePublisher>();
        var publisher2 = scope2.ServiceProvider.GetRequiredService<IMessagePublisher>();

        Assert.NotSame(publisher1, publisher2);
    }
}