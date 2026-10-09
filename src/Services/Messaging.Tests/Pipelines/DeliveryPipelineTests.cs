namespace Messaging.Tests.Pipelines;

public class DeliveryPipelineTests
{
    [Fact]
    public async Task RunDelivery_WithoutMiddlewares_CallsTerminalDirectly()
    {
        var services = new ServiceCollection();
        new MessagingBuilder(services);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var context = DeliveryContexts.Sample();
        var calls = 0;

        var expected = new ConsumeOutcome(MessageConsumeResult.Success);

        DeliveryDelegate terminal = () =>
        {
            calls++;
            return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success));
        };

        var outcome = await MessagingPipeline.RunDelivery(context, scope.ServiceProvider, terminal,
            CancellationToken.None);

        Assert.Equal(1, calls);
        Assert.Equal(expected, outcome);
    }

    [Fact]
    public async Task RunDelivery_RunsMiddlewaresOutsideInAroundTerminal()
    {
        var services = new ServiceCollection();
        var log = new TraceLog();
        services.AddSingleton(log);
        new MessagingBuilder(services)
            .AddDeliveryMiddleware<ProbeA>()
            .AddDeliveryMiddleware<ProbeB>()
            .AddDeliveryMiddleware<ProbeC>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var outcome = await MessagingPipeline.RunDelivery(DeliveryContexts.Sample(), scope.ServiceProvider,
            () =>
            {
                log.Entries.Add("consumer");
                return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success));
            },
            CancellationToken.None);

        Assert.Equal(MessageConsumeResult.Success, outcome.Kind);
        Assert.Equal(
            ["A:before", "B:before", "C:before", "consumer", "C:after", "B:after", "A:after"],
            log.Entries);
    }

    [Fact]
    public async Task RunDelivery_MiddlewareRegistrationOrder_DefinesNestingOrder()
    {
        var log = new TraceLog();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        new MessagingBuilder(services)
            .AddDeliveryMiddleware<ProbeC>()
            .AddDeliveryMiddleware<ProbeB>()
            .AddDeliveryMiddleware<ProbeA>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        await MessagingPipeline.RunDelivery(DeliveryContexts.Sample(), scope.ServiceProvider,
            () =>
            {
                log.Entries.Add("terminal");
                return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success));
            },
            CancellationToken.None);

        Assert.Equal(["C:before", "B:before", "A:before", "terminal", "A:after", "B:after", "C:after"],
            log.Entries);
    }

    [Fact]
    public async Task RunDelivery_MiddlewareShortCircuits_SkipsInnerMiddlewaresAndTerminal()
    {
        var services = new ServiceCollection();
        new MessagingBuilder(services)
            .AddDeliveryMiddleware<ShortCircuitDeliveryMiddleware>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var calls = 0;

        var outcome = await MessagingPipeline.RunDelivery(DeliveryContexts.Sample(), scope.ServiceProvider,
            () =>
            {
                calls++;
                return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success));
            },
            CancellationToken.None);

        Assert.Equal(0, calls);
        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
    }

    [Fact]
    public async Task RunDelivery_TerminalReturnsRetry_OutermostMiddlewareSeesRetry()
    {
        var outcomes = new OutcomeLog();
        var services = new ServiceCollection();
        services.AddSingleton(outcomes);
        new MessagingBuilder(services).AddDeliveryMiddleware<OutcomeCaptureMiddleware>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var expected = new ConsumeOutcome(MessageConsumeResult.Retry, "boom");

        var result = await MessagingPipeline.RunDelivery(DeliveryContexts.Sample(), scope.ServiceProvider,
            () => Task.FromResult(expected),
            CancellationToken.None);

        Assert.Equal(expected, Assert.Single(outcomes.Seen));
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task RunDelivery_MiddlewareOverridesOutcome_CallerGetsOverriddenOutcome()
    {
        var services = new ServiceCollection();
        new MessagingBuilder(services)
            .AddDeliveryMiddleware<OverrideOutcomeDeliveryMiddleware>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var outcome = await MessagingPipeline.RunDelivery(DeliveryContexts.Sample(), scope.ServiceProvider,
            () => Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success)),
            CancellationToken.None);

        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
    }

    [Fact]
    public async Task RunDelivery_TerminalThrows_ExceptionPropagatesThroughMiddlewares()
    {
        var log = new TraceLog();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        new MessagingBuilder(services)
            .AddDeliveryMiddleware<ProbeA>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var outcomeTask = MessagingPipeline.RunDelivery(DeliveryContexts.Sample(), scope.ServiceProvider,
            () => throw new InvalidOperationException("Terminal exception"),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => outcomeTask);
        Assert.Equal(["A:before"], log.Entries);
    }

    [Fact]
    public async Task RunDelivery_MiddlewaresReceiveTypedMessageAndContext()
    {
        var captured = new CapturedContext();
        var services = new ServiceCollection();
        services.AddSingleton(captured);
        new MessagingBuilder(services).AddDeliveryMiddleware<ContextCaptureMiddleware>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = DeliveryContexts.Sample();

        await MessagingPipeline.RunDelivery(context, scope.ServiceProvider,
            () => Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success)),
            CancellationToken.None);

        var seen = Assert.IsType<DeliveryContext<TestMessage>>(captured.Last);
        Assert.Same(context, seen);
    }

    [Fact]
    public async Task RunDelivery_ShouldMutateHeaders()
    {
        var services = new ServiceCollection();
        var log = new TraceLog();
        var a = "mutated";
        services.AddSingleton(log);
        services.AddSingleton(a);
        new MessagingBuilder(services)
            .AddDeliveryMiddleware<MutateMiddleware>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var context = DeliveryContexts.Sample();
        context.Headers[a] = "value";

        var hdrs = context.Headers;

        await MessagingPipeline.RunDelivery(context, scope.ServiceProvider,
            () =>
            {
                log.Entries.Add("consumer");
                context.Headers["consumer"] = "value";
                hdrs = context.Headers;
                return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success));
            },
            CancellationToken.None);

        Assert.NotEqual("value", hdrs[a]);
    }

    [Fact]
    public async Task RunDelivery_ScopedMiddleware_NewInstancePerScope()
    {
        var services = new ServiceCollection();
        services.AddScoped<OutcomeLog>();
        new MessagingBuilder(services)
            .AddDeliveryMiddleware<OutcomeCaptureMiddleware>();

        await using var provider = services.BuildServiceProvider();
        await using var scope1 = provider.CreateAsyncScope();
        await using var scope2 = provider.CreateAsyncScope();

        var outcome1 = scope1.ServiceProvider.GetRequiredService<OutcomeLog>();
        var outcome2 = scope2.ServiceProvider.GetRequiredService<OutcomeLog>();

        var context = DeliveryContexts.Sample();

        var outcomeTask1 = MessagingPipeline.RunDelivery(context, scope1.ServiceProvider,
            () => Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success)),
            CancellationToken.None);

        var outcomeTask2 = MessagingPipeline.RunDelivery(context, scope2.ServiceProvider,
            () => Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success)),
            CancellationToken.None);

        await outcomeTask1;
        await outcomeTask2;

        Assert.NotSame(outcome1, outcome2);
        Assert.Equal(outcome1.Seen, outcome2.Seen);
    }

    [Fact]
    public async Task RunDelivery_ScopedMiddlewareAndConsumer_ShareScopedDependencyInSameScope()
    {
        var seen = new ScopedValues();
        var services = new ServiceCollection();
        services.AddSingleton(seen);
        services.AddScoped<ScopedValue>();
        new MessagingBuilder(services).AddDeliveryMiddleware<ScopedTouchMiddleware>();

        await using var provider = services.BuildServiceProvider();
        await using var scope1 = provider.CreateAsyncScope();
        await using var scope2 = provider.CreateAsyncScope();

        foreach (var scope in new[] { scope1, scope2 })
        {
            await MessagingPipeline.RunDelivery(DeliveryContexts.Sample(), scope.ServiceProvider,
                () =>
                {
                    seen.Items.Add(scope.ServiceProvider.GetRequiredService<ScopedValue>());
                    return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success));
                },
                CancellationToken.None);
        }

        Assert.Equal(4, seen.Items.Count);
        Assert.Same(seen.Items[0], seen.Items[1]);
        Assert.Same(seen.Items[2], seen.Items[3]);
        Assert.NotSame(seen.Items[0], seen.Items[2]);
    }

    [Fact]
    public async Task RunDelivery_CalledTwice_DoesNotReuseFirstCallState()
    {
        var captured = new CapturedContext();
        var services = new ServiceCollection();
        services.AddSingleton(captured);
        new MessagingBuilder(services).AddDeliveryMiddleware<ContextCaptureMiddleware>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var first = DeliveryContexts.Sample() with { MessageId = "first" };
        var second = DeliveryContexts.Sample() with { MessageId = "second" };
        var firstCalls = 0;
        var secondCalls = 0;

        await MessagingPipeline.RunDelivery(first, scope.ServiceProvider,
            () =>
            {
                firstCalls++;
                return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success));
            },
            CancellationToken.None);

        var outcome = await MessagingPipeline.RunDelivery(second, scope.ServiceProvider,
            () =>
            {
                secondCalls++;
                return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Retry, "second"));
            },
            CancellationToken.None);

        Assert.Equal(1, firstCalls);
        Assert.Equal(1, secondCalls);
        Assert.Equal(MessageConsumeResult.Retry, outcome.Kind);
        Assert.Same(second, Assert.IsType<DeliveryContext<TestMessage>>(captured.Last));
    }
}
