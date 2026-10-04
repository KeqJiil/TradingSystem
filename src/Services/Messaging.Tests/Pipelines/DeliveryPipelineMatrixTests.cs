using Messaging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Messaging.Tests.Pipelines;

public class DeliveryPipelineMatrixTests
{
    private sealed record TestMessage(string Value);

    private static DeliveryContext<TestMessage> CreateContext()
    {
        return new DeliveryContext<TestMessage>(
            "topic", "group", 0, 0, "message-id", nameof(TestMessage),
            new TestMessage("value"), DateTimeOffset.UtcNow, new Dictionary<string, string>());
    }

    [Fact]
    public async Task RunDelivery_WithoutMiddlewares_CallsTerminalDirectly()
    {
        var services = new ServiceCollection();
        new MessagingBuilder(services);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var context = CreateContext();
        var calls = 0;

        var expected = new ConsumeOutcome(MessageConsumeResult.Success);

        DeliveryDelegate terminal = () =>
        {
            calls++;
            return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success));
        };

        var outcomeTask = MessagingPipeline.RunDelivery(context, scope.ServiceProvider,
            terminal,
            CancellationToken.None);

        var outcome = await outcomeTask;

        Assert.Equal(1, calls);
        Assert.Equal(expected, outcome);
    }

    [Fact]
    public async Task RunDelivery_MiddlewareShortCircuits_SkipsInnerMiddlewaresAndTerminal()
    {
        var services = new ServiceCollection();
        new MessagingBuilder(services)
            .AddDeliveryMiddleware<FailureMiddleware>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var context = CreateContext();
        var calls = 0;

        var outcomeTask = MessagingPipeline.RunDelivery(context, scope.ServiceProvider,
            () =>
            {
                calls++;
                return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success));
            },
            CancellationToken.None);

        var outcome = await outcomeTask;

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

        var result = await MessagingPipeline.RunDelivery(CreateContext(), scope.ServiceProvider,
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
            .AddDeliveryMiddleware<FailureMiddleware2>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var context = CreateContext();

        var outcomeTask = MessagingPipeline.RunDelivery(context, scope.ServiceProvider,
            () => { return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success)); },
            CancellationToken.None);

        var outcome = await outcomeTask;

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

        var context = CreateContext();

        var outcomeTask = MessagingPipeline.RunDelivery(context, scope.ServiceProvider,
            () => throw new InvalidOperationException("Terminal exception"),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => outcomeTask);
        Assert.Equal(new string[] { "A:before" }, log.Entries);
    }

    [Fact(Skip =
        "Probe captures context. Assert Message, Topic, ConsumerGroup, Partition, Offset, MessageId equal the input.")]
    public void RunDelivery_MiddlewaresReceiveTypedMessageAndContext()
    {
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

        var context = CreateContext();

        var outcomeTask1 = MessagingPipeline.RunDelivery(context, scope1.ServiceProvider,
            () => { return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success)); },
            CancellationToken.None);

        var outcomeTask2 = MessagingPipeline.RunDelivery(context, scope2.ServiceProvider,
            () => { return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success)); },
            CancellationToken.None);

        await outcomeTask1;
        await outcomeTask2;

        Assert.NotSame(outcome1, outcome2);
        Assert.Equal(outcome1.Seen, outcome2.Seen);
    }

    [Fact(Skip =
        "Scoped dependency used by middleware and terminal. Assert same instance within one scope, different across scopes.")]
    public Task RunDelivery_ScopedMiddlewareAndConsumer_ShareScopedDependencyInSameScope()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip = "Register C, B, A. Assert C is outermost, then B, then A.")]
    public Task RunDelivery_MiddlewareRegistrationOrder_DefinesNestingOrder()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip =
        "Run with two different contexts and terminals. Assert second run uses second context and terminal (guards against pipeline caching).")]
    public Task RunDelivery_CalledTwice_DoesNotReuseFirstCallState()
    {
        return Task.CompletedTask;
    }
}