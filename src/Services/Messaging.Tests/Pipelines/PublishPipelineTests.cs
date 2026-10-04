using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Messaging.Tests.Pipelines;

public class PublishPipelineTests
{
    private static ServiceProvider BuildProvider(Action<MessagingBuilder>? configure = null, TraceLog? log = null,
        PublishOutcomeLog? outcomes = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(log ?? new TraceLog());
        services.AddSingleton(outcomes ?? new PublishOutcomeLog());
        configure?.Invoke(new MessagingBuilder(services));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task RunPublish_WithoutMiddlewares_CallsTerminalWithContextValues()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var terminal = new RecordingPublishTerminal();
        var context = PublishContexts.Sample(new Dictionary<string, string> { ["x"] = "y" });

        var outcome = await MessagingPublishPipeline.RunPublish(context, scope.ServiceProvider, terminal,
            CancellationToken.None);

        Assert.True(outcome.IsSuccessful);
        var call = Assert.Single(terminal.Calls);
        Assert.Equal(context.Topic, call.Topic);
        Assert.Equal(context.KeyId, call.Key);
        Assert.Equal(context.MessageId, call.MessageId);
        Assert.Equal(context.Message, call.Payload);
        Assert.Equal("y", call.Headers["x"]);
    }

    [Fact]
    public async Task RunPublish_RunsMiddlewaresOutsideInAroundTerminal()
    {
        var log = new TraceLog();
        using var provider = BuildProvider(b => b
            .AddPublishMiddleware<PublishProbeA>()
            .AddPublishMiddleware<PublishProbeB>()
            .AddPublishMiddleware<PublishProbeC>(), log);
        using var scope = provider.CreateScope();
        var terminal = new RecordingPublishTerminal();

        await MessagingPublishPipeline.RunPublish(PublishContexts.Sample(), scope.ServiceProvider, terminal,
            CancellationToken.None);

        Assert.Equal(["A:before", "B:before", "C:before", "C:after", "B:after", "A:after"], log.Entries);
        Assert.Single(terminal.Calls);
    }

    [Fact]
    public async Task RunPublish_MiddlewareChangesHeaders_ChangedHeadersReachTerminal()
    {
        using var provider = BuildProvider(b => b.AddPublishMiddleware<HeaderStampPublishMiddleware>());
        using var scope = provider.CreateScope();
        var terminal = new RecordingPublishTerminal();

        await MessagingPublishPipeline.RunPublish(PublishContexts.Sample(), scope.ServiceProvider, terminal,
            CancellationToken.None);

        var call = Assert.Single(terminal.Calls);
        Assert.Equal(HeaderStampPublishMiddleware.HeaderValue, call.Headers[HeaderStampPublishMiddleware.HeaderName]);
    }

    [Fact]
    public async Task RunPublish_MiddlewareShortCircuits_TerminalNotCalled()
    {
        using var provider = BuildProvider(b => b.AddPublishMiddleware<ShortCircuitPublishMiddleware>());
        using var scope = provider.CreateScope();
        var terminal = new RecordingPublishTerminal();

        var outcome = await MessagingPublishPipeline.RunPublish(PublishContexts.Sample(), scope.ServiceProvider,
            terminal, CancellationToken.None);

        Assert.False(outcome.IsSuccessful);
        Assert.Equal(ShortCircuitPublishMiddleware.Error, outcome.ErrorMessage);
        Assert.Empty(terminal.Calls);
    }

    [Fact]
    public async Task RunPublish_TerminalFails_OutcomeFlowsThroughMiddlewares()
    {
        var outcomes = new PublishOutcomeLog();
        await using var provider = BuildProvider(b => b.AddPublishMiddleware<OutcomeCapturePublishMiddleware>(),
            outcomes: outcomes);
        using var scope = provider.CreateScope();
        var terminal = new RecordingPublishTerminal { Result = new PublishOutcome(false, "kafka down") };

        var outcome = await MessagingPublishPipeline.RunPublish(PublishContexts.Sample(), scope.ServiceProvider,
            terminal, CancellationToken.None);

        Assert.False(outcome.IsSuccessful);
        Assert.Equal("kafka down", outcome.ErrorMessage);
        Assert.Equal(outcome, Assert.Single(outcomes.Seen));
    }

    [Fact]
    public void RunPublish_ScopedMiddleware_NewInstancePerScope()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddMessaging(
            builder => { builder.AddPublishMiddleware<EmptyPublishMiddleware>(); },
            options => { options.BootstrapServers = "localhost:9092"; });

        var provider = services.BuildServiceProvider();
        var scope1 = provider.CreateScope();
        var scope2 = provider.CreateScope();

        var publisher1 = scope1.ServiceProvider.GetRequiredService<IMessagePublisher>();
        var publisher2 = scope2.ServiceProvider.GetRequiredService<IMessagePublisher>();

        Assert.NotSame(publisher1, publisher2);
    }
}