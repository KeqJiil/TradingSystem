using Messaging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Messaging.Tests.Pipelines;

public class DeliveryPipelineTests
{
    private sealed record TestMessage(string Value);

    private static DeliveryContext<TestMessage> CreateContext()
    {
        return new DeliveryContext<TestMessage>(
            "topic", "group", 0, 0, "message-id", nameof(TestMessage),
            new TestMessage("value"), DateTimeOffset.UtcNow, new Dictionary<string, string>());
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

        var outcome = await MessagingPipeline.RunDelivery(CreateContext(), scope.ServiceProvider,
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

        var context = CreateContext();
        context.Headers[a] = "value";

        var hdrs = context.Headers;

        var outcomeTask = MessagingPipeline.RunDelivery(context, scope.ServiceProvider,
            () =>
            {
                log.Entries.Add("consumer");
                context.Headers["consumer"] = "value";
                hdrs = context.Headers;
                return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success));
            },
            CancellationToken.None);

        await outcomeTask;

        Assert.NotEqual("value", hdrs[a]);
    }
}