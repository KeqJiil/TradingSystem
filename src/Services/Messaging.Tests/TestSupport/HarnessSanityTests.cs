using Messaging.Abstractions;
using Xunit;

namespace Messaging.Tests.TestSupport;

public class HarnessSanityTests
{
    [Fact]
    public async Task Publish_ReachesRecordingTerminalWithTopicKeyAndPayload()
    {
        using var harness = new PublisherHarness();

        var outcome = await harness.PublishAsync(new SampleMessage("a", 1), PublisherHarness.DefaultOptions());

        Assert.True(outcome.IsSuccessful);
        var call = Assert.Single(harness.Terminal.Calls);
        Assert.Equal(PublisherHarness.Topic, call.Topic);
        Assert.Equal("key", call.Key);
        Assert.Equal(new SampleMessage("a", 1), call.Payload);
    }

    [Fact]
    public async Task Publish_RunsMiddlewaresInOrderAndHeadersReachTerminal()
    {
        using var harness = new PublisherHarness(b => b
            .AddPublishMiddleware<PublishProbeA>()
            .AddPublishMiddleware<HeaderStampPublishMiddleware>()
            .AddPublishMiddleware<PublishProbeB>());

        await harness.PublishAsync(new SampleMessage("a", 1), PublisherHarness.DefaultOptions());

        Assert.Equal(["A:before", "B:before", "B:after", "A:after"], harness.Log.Entries);
        var call = Assert.Single(harness.Terminal.Calls);
        Assert.Equal(HeaderStampPublishMiddleware.HeaderValue, call.Headers[HeaderStampPublishMiddleware.HeaderName]);
    }

    [Fact]
    public async Task Publish_TerminalFails_OutcomeFlowsBackThroughMiddleware()
    {
        using var harness = new PublisherHarness(b => b.AddPublishMiddleware<OutcomeCapturePublishMiddleware>());
        harness.Terminal.Result = new PublishOutcome(false, "kafka down");

        var outcome = await harness.PublishAsync(new SampleMessage("a", 1), PublisherHarness.DefaultOptions());

        Assert.False(outcome.IsSuccessful);
        Assert.Equal("kafka down", outcome.ErrorMessage);
        Assert.Equal("kafka down", Assert.Single(harness.Outcomes.Seen).ErrorMessage);
    }

    [Fact]
    public async Task Publish_ShortCircuitMiddleware_TerminalNotCalled()
    {
        using var harness = new PublisherHarness(b => b.AddPublishMiddleware<ShortCircuitPublishMiddleware>());

        var outcome = await harness.PublishAsync(new SampleMessage("a", 1), PublisherHarness.DefaultOptions());

        Assert.False(outcome.IsSuccessful);
        Assert.Equal(ShortCircuitPublishMiddleware.Error, outcome.ErrorMessage);
        Assert.Empty(harness.Terminal.Calls);
    }

    [Fact]
    public async Task Publish_ScopedMiddleware_NewInstancePerScope()
    {
        using var harness = new PublisherHarness(b => b.AddPublishMiddleware<InstanceTrackingPublishMiddleware>());

        await harness.PublishAsync(new SampleMessage("a", 1), PublisherHarness.DefaultOptions());
        await harness.PublishAsync(new SampleMessage("b", 2), PublisherHarness.DefaultOptions());

        Assert.Equal(2, harness.Instances.Instances.Count);
        Assert.NotSame(harness.Instances.Instances[0], harness.Instances.Instances[1]);
    }
}