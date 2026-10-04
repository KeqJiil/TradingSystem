using Messaging.Abstractions;
using Messaging.Serializers;
using Messaging.Tests.TestSupport;
using Xunit;

namespace Messaging.Tests.Publisher;

public class MessagingPublisherTests
{
    private record TestMessage(string Name, int Value);

    [Fact]
    public async Task PublishAsync_NullOptions_ReturnsFailedOutcome()
    {
        using var harness = new PublisherHarness(b => { b.AddMessage<TestMessage>("a", new JsonDefaultSerializer()); });

        var res = await harness.PublishAsync(new SampleMessage("a", 1), null!);

        Assert.False(res.IsSuccessful);
    }

    [Fact]
    public async Task PublishAsync_UsesTopicAndKeyFromOptions()
    {
        using var harness = new PublisherHarness(b => { b.AddMessage<TestMessage>("a", new JsonDefaultSerializer()); });

        var res = await harness.PublishAsync(new SampleMessage("a", 1),
            new PublishOptions("123", "a", new Dictionary<string, string>()));

        var call = harness.Terminal.Calls;

        Assert.Single(call);
        Assert.Equal("a", call[0].Topic);
        Assert.Equal("123", call[0].Key);
        Assert.True(res.IsSuccessful);
        Assert.Null(res.ErrorMessage);
    }

    [Fact]
    public async Task PublishAsync_StampsMessageIdHeaders()
    {
        using var harness = new PublisherHarness(b => { b.AddMessage<TestMessage>("a", new JsonDefaultSerializer()); });

        var res = await harness.PublishAsync(new SampleMessage("a", 1),
            new PublishOptions("123", "a", new Dictionary<string, string>()));

        var call = harness.Terminal.Calls;

        Assert.NotEqual(call[0].MessageId, Guid.Empty.ToString());
    }

    [Fact]
    public async Task PublishAsync_GeneratesUniqueMessageIdPerCall()
    {
        using var harness = new PublisherHarness(b => { b.AddMessage<TestMessage>("a", new JsonDefaultSerializer()); });

        var res1 = await harness.PublishAsync(new SampleMessage("a", 1),
            new PublishOptions("123", "a", new Dictionary<string, string>()));
        var res2 = await harness.PublishAsync(new SampleMessage("a", 1),
            new PublishOptions("123", "a", new Dictionary<string, string>()));

        var call = harness.Terminal.Calls;

        Assert.NotEqual(call[0].MessageId, call[1].MessageId);
    }

    [Fact]
    public async Task PublishAsync_DoesNotMutateCallerHeaders()
    {
        using var harness = new PublisherHarness(b => b.AddPublishMiddleware<HeaderStampPublishMiddleware>());

        var headers = new Dictionary<string, string> { ["x"] = "y" };

        var res = await harness.PublishAsync(new SampleMessage("a", 1),
            new PublishOptions("123", "a", headers));

        var calls = harness.Terminal.Calls;

        Assert.Single(headers);
        Assert.True(harness.Terminal.Calls[0].Headers.ContainsKey("traceparent"));
    }

    [Fact]
    public async Task PublishAsync_RunsPublishMiddlewaresBeforeTerminal()
    {
        using var harness = new PublisherHarness(b => b
            .AddMessage<TestMessage>("a", new JsonDefaultSerializer())
            .AddPublishMiddleware<PublishProbeA>()
            .AddPublishMiddleware<PublishProbeB>());

        var res = await harness.PublishAsync(new SampleMessage("a", 1),
            new PublishOptions("123", "a", new Dictionary<string, string>()));

        Assert.Equal(new[] { "A:before", "B:before", "B:after", "A:after" }, harness.Log.Entries);
    }

    [Fact]
    public async Task PublishAsync_TerminalFails_ReturnsFailedOutcomeWithReason()
    {
        using var harness = new PublisherHarness();
        harness.Terminal.Result = new PublishOutcome(false, "kafka down");

        var outcome = await harness.PublishAsync(new SampleMessage("a", 1), PublisherHarness.DefaultOptions());

        Assert.False(outcome.IsSuccessful);
        Assert.Equal("kafka down", outcome.ErrorMessage);
    }

    [Fact(Skip = "Document current behavior for a type never passed to AddMessage.")]
    public async Task PublishAsync_UnregisteredMessageType_Behavior()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "Serializer returns failure. Document current behavior (outcome or exception).")]
    public Task PublishAsync_SerializationFails_Behavior()
    {
        return Task.CompletedTask;
    }
}