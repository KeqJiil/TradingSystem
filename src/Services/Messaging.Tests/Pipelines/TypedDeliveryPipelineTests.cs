namespace Messaging.Tests.Pipelines;

public class TypedDeliveryPipelineTests
{
    private static readonly DeliveryDelegate SuccessTerminal =
        () => Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success));

    [Fact]
    public async Task RunDelivery_TypedMiddleware_RunsOnlyForItsMessageType()
    {
        var log = new TraceLog();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        new MessagingBuilder(services)
            .AddDeliveryMiddleware<TestMessage, TypedProbeX>()
            .AddDeliveryMiddleware<TestMessage2, TypedProbeOther>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        await MessagingPipeline.RunDelivery(DeliveryContexts.Sample(), scope.ServiceProvider, SuccessTerminal,
            CancellationToken.None);

        Assert.Equal(["X:before", "X:after"], log.Entries);
    }

    [Fact]
    public async Task RunDelivery_MessageWithoutTypedMiddleware_RunsOnlyCommonOnes()
    {
        var log = new TraceLog();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        new MessagingBuilder(services)
            .AddDeliveryMiddleware<ProbeA>()
            .AddDeliveryMiddleware<TestMessage, TypedProbeX>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        await MessagingPipeline.RunDelivery(DeliveryContexts.Sample2(), scope.ServiceProvider, SuccessTerminal,
            CancellationToken.None);

        Assert.Equal(["A:before", "A:after"], log.Entries);
    }

    [Fact]
    public async Task RunDelivery_TypedMiddlewareRunsInsideCommonOnes_RegardlessOfRegistrationOrder()
    {
        var log = new TraceLog();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        new MessagingBuilder(services)
            .AddDeliveryMiddleware<TestMessage, TypedProbeX>()
            .AddDeliveryMiddleware<ProbeA>()
            .AddDeliveryMiddleware<ProbeB>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        await MessagingPipeline.RunDelivery(DeliveryContexts.Sample(), scope.ServiceProvider,
            () =>
            {
                log.Entries.Add("consumer");
                return SuccessTerminal();
            },
            CancellationToken.None);

        Assert.Equal(
            ["A:before", "B:before", "X:before", "consumer", "X:after", "B:after", "A:after"],
            log.Entries);
    }

    [Fact]
    public async Task RunDelivery_TypedMiddlewares_RegistrationOrderDefinesNesting()
    {
        var log = new TraceLog();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        new MessagingBuilder(services)
            .AddDeliveryMiddleware<TestMessage, TypedProbeY>()
            .AddDeliveryMiddleware<TestMessage, TypedProbeX>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        await MessagingPipeline.RunDelivery(DeliveryContexts.Sample(), scope.ServiceProvider, SuccessTerminal,
            CancellationToken.None);

        Assert.Equal(["Y:before", "X:before", "X:after", "Y:after"], log.Entries);
    }
}
