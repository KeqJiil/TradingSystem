using Messaging.Serializers;

namespace Messaging.Tests.Publisher;

public class MessagingPublisherTests
{
    private sealed record UnregisteredMessage(string Value);
    
    [Fact]
    public async Task PublishAsync_UsesRegisteredTopicAndKeyFromOptions()
    {
        using var harness = new PublisherHarness(b => { b.AddMessage<TestMessage>("a", new JsonDefaultSerializer()); });

        var res = await harness.PublishAsync(new SampleMessage("a", 1),
            new PublishOptions("123"));

        var call = harness.Terminal.Calls;

        Assert.Single(call);
        Assert.Equal(PublisherHarness.Topic, call[0].Topic);
        Assert.Equal("123", call[0].Key);
        Assert.True(res.IsSuccessful);
        Assert.Null(res.ErrorMessage);
    }

    [Fact]
    public async Task PublishAsync_StampsMessageIdHeaders()
    {
        using var harness = new PublisherHarness(b => { b.AddMessage<TestMessage>("a", new JsonDefaultSerializer()); });

        var res = await harness.PublishAsync(new SampleMessage("a", 1),
            new PublishOptions("123"));

        var call = harness.Terminal.Calls;

        Assert.NotEqual(call[0].MessageId, Guid.Empty.ToString());
    }

    [Fact]
    public async Task PublishAsync_GeneratesUniqueMessageIdPerCall()
    {
        using var harness = new PublisherHarness(b => { b.AddMessage<TestMessage>("a", new JsonDefaultSerializer()); });

        var res1 = await harness.PublishAsync(new SampleMessage("a", 1),
            new PublishOptions("123"));
        var res2 = await harness.PublishAsync(new SampleMessage("a", 1),
            new PublishOptions("123"));

        var call = harness.Terminal.Calls;

        Assert.NotEqual(call[0].MessageId, call[1].MessageId);
    }

    [Fact]
    public async Task PublishAsync_NullHeaders_PublishesWithEmptyHeaders()
    {
        using var harness = new PublisherHarness();

        var res = await harness.PublishAsync(new SampleMessage("a", 1), new PublishOptions { KeyId = "123" });

        Assert.True(res.IsSuccessful);
        Assert.Empty(Assert.Single(harness.Terminal.Calls).Headers);
    }

    [Fact]
    public async Task PublishAsync_DoesNotMutateCallerHeaders()
    {
        using var harness = new PublisherHarness(b => b.AddPublishMiddleware<HeaderStampPublishMiddleware>());

        var headers = new Dictionary<string, string> { ["x"] = "y" };

        var res = await harness.PublishAsync(new SampleMessage("a", 1),
            new PublishOptions("123", headers));

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
            new PublishOptions("123"));

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

    [Fact]
    public async Task PublishAsync_UnregisteredMessageType_ThrowsWithoutCallingTerminal()
    {
        using var harness = new PublisherHarness();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.PublishAsync(new UnregisteredMessage("x"), PublisherHarness.DefaultOptions()));

        Assert.Contains(nameof(UnregisteredMessage), exception.Message);
        Assert.Empty(harness.Terminal.Calls);
    }
}