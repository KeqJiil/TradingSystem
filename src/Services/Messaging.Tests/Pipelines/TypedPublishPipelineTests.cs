namespace Messaging.Tests.Pipelines;

public class TypedPublishPipelineTests
{
    private static ServiceProvider BuildProvider(Action<MessagingBuilder> configure, TraceLog log)
    {
        var services = new ServiceCollection();
        services.AddSingleton(log);
        configure(new MessagingBuilder(services));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task RunPublish_TypedMiddleware_RunsOnlyForItsMessageType()
    {
        var log = new TraceLog();
        using var provider = BuildProvider(b => b
            .AddPublishMiddleware<SampleMessage, TypedPublishProbeX>()
            .AddPublishMiddleware<TestMessage, TypedPublishProbeOther>(), log);
        using var scope = provider.CreateScope();
        var terminal = new RecordingPublishTerminal();

        await MessagingPublishPipeline.RunPublish(PublishContexts.Sample(), scope.ServiceProvider, terminal,
            CancellationToken.None);

        Assert.Equal(["X:before", "X:after"], log.Entries);
        Assert.Single(terminal.Calls);
    }

    [Fact]
    public async Task RunPublish_MessageWithoutTypedMiddleware_RunsOnlyCommonOnes()
    {
        var log = new TraceLog();
        using var provider = BuildProvider(b => b
            .AddPublishMiddleware<PublishProbeA>()
            .AddPublishMiddleware<SampleMessage, TypedPublishProbeX>(), log);
        using var scope = provider.CreateScope();
        var terminal = new RecordingPublishTerminal();

        await MessagingPublishPipeline.RunPublish(PublishContexts.SampleOther(), scope.ServiceProvider, terminal,
            CancellationToken.None);

        Assert.Equal(["A:before", "A:after"], log.Entries);
    }

    [Fact]
    public async Task RunPublish_TypedMiddlewareRunsInsideCommonOnes_RegardlessOfRegistrationOrder()
    {
        var log = new TraceLog();
        using var provider = BuildProvider(b => b
            .AddPublishMiddleware<SampleMessage, TypedPublishProbeX>()
            .AddPublishMiddleware<PublishProbeA>()
            .AddPublishMiddleware<PublishProbeB>(), log);
        using var scope = provider.CreateScope();
        var terminal = new RecordingPublishTerminal();

        await MessagingPublishPipeline.RunPublish(PublishContexts.Sample(), scope.ServiceProvider, terminal,
            CancellationToken.None);

        Assert.Equal(["A:before", "B:before", "X:before", "X:after", "B:after", "A:after"], log.Entries);
    }

    [Fact]
    public async Task RunPublish_TypedMiddlewares_RegistrationOrderDefinesNesting()
    {
        var log = new TraceLog();
        using var provider = BuildProvider(b => b
            .AddPublishMiddleware<SampleMessage, TypedPublishProbeY>()
            .AddPublishMiddleware<SampleMessage, TypedPublishProbeX>(), log);
        using var scope = provider.CreateScope();
        var terminal = new RecordingPublishTerminal();

        await MessagingPublishPipeline.RunPublish(PublishContexts.Sample(), scope.ServiceProvider, terminal,
            CancellationToken.None);

        Assert.Equal(["Y:before", "X:before", "X:after", "Y:after"], log.Entries);
    }
}
