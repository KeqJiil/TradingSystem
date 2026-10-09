using System.Diagnostics;
using Messaging.Middlewares;

namespace Messaging.Tests.Tracing;

[Collection("Tracing")]
public class TracingPublishMiddlewareTests
{
    private const string AmbientSource = "test-ambient";
    private static readonly string SendName = $"send {PublisherHarness.Topic}";

    private static async Task<PublishOutcome> Run(RecordingPublishTerminal terminal,
        Dictionary<string, string>? headers = null)
    {
        var services = new ServiceCollection();
        new MessagingBuilder(services).AddPublishMiddleware<TracingPublishMiddleware>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        return await MessagingPublishPipeline.RunPublish(PublishContexts.Sample(headers), scope.ServiceProvider,
            terminal, CancellationToken.None);
    }

    private static IReadOnlyDictionary<string, string> SentHeaders(RecordingPublishTerminal terminal)
    {
        return Assert.Single(terminal.Calls).Headers;
    }

    [Fact]
    public async Task OnPublish_StartsProducerActivityWithMessagingTags()
    {
        using var recorder = new ActivityRecorder();

        await Run(new RecordingPublishTerminal());

        var activity = recorder.Single(SendName);
        Assert.Equal(ActivityKind.Producer, activity.Kind);
        Assert.Equal("kafka", activity.GetTagItem("messaging.system"));
        Assert.Equal(PublisherHarness.Topic, activity.GetTagItem("messaging.destination.name"));
        Assert.Equal("key", activity.GetTagItem("messaging.kafka.message.key"));
        Assert.Equal("message-id", activity.GetTagItem("messaging.message.id"));
        Assert.Equal(nameof(SampleMessage), activity.GetTagItem("messaging.message.type"));
    }

    [Fact]
    public async Task OnPublish_InjectsTraceParentOfSendActivity()
    {
        using var recorder = new ActivityRecorder();
        var terminal = new RecordingPublishTerminal();

        await Run(terminal);

        Assert.Equal(recorder.Single(SendName).Id, SentHeaders(terminal)[MessagingHeaders.TraceParent]);
    }

    [Fact]
    public async Task OnPublish_SendActivityIsChildOfAmbientActivity()
    {
        using var recorder = new ActivityRecorder();
        using var ambientRecorder = new ActivityRecorder(AmbientSource);
        using var ambientSource = new ActivitySource(AmbientSource);
        using var ambient = ambientSource.StartActivity("ambient")!;
        var terminal = new RecordingPublishTerminal();

        await Run(terminal);

        var send = recorder.Single(SendName);
        Assert.Equal(ambient.SpanId, send.ParentSpanId);
        Assert.Equal(ambient.TraceId, send.TraceId);
    }

    [Fact]
    public async Task OnPublish_AmbientTraceState_IsInjected()
    {
        using var recorder = new ActivityRecorder();
        using var ambientRecorder = new ActivityRecorder(AmbientSource);
        using var ambientSource = new ActivitySource(AmbientSource);
        using var ambient = ambientSource.StartActivity("ambient")!;
        ambient.TraceStateString = "k=v";
        var terminal = new RecordingPublishTerminal();

        await Run(terminal);

        Assert.Equal("k=v", SentHeaders(terminal)[MessagingHeaders.TraceState]);
    }

    [Fact]
    public async Task OnPublish_NoListener_PropagatesAmbientTraceParent()
    {
        using var ambientRecorder = new ActivityRecorder(AmbientSource);
        using var ambientSource = new ActivitySource(AmbientSource);
        using var ambient = ambientSource.StartActivity("ambient")!;
        var terminal = new RecordingPublishTerminal();

        await Run(terminal);

        Assert.Equal(ambient.Id, SentHeaders(terminal)[MessagingHeaders.TraceParent]);
    }

    [Fact]
    public async Task OnPublish_NoActivityAtAll_DoesNotAddTraceHeaders()
    {
        var terminal = new RecordingPublishTerminal();

        await Run(terminal);

        var headers = SentHeaders(terminal);
        Assert.DoesNotContain(MessagingHeaders.TraceParent, headers.Keys);
        Assert.DoesNotContain(MessagingHeaders.TraceState, headers.Keys);
    }

    [Fact]
    public async Task OnPublish_AmbientCorrelation_IsAddedToHeaders()
    {
        MessagingCorrelation.CorrelationId = "corr-1";
        var terminal = new RecordingPublishTerminal();

        await Run(terminal);

        Assert.Equal("corr-1", SentHeaders(terminal)[MessagingHeaders.CorrelationId]);
    }

    [Fact]
    public async Task OnPublish_CallerCorrelationHeader_IsNotOverwritten()
    {
        MessagingCorrelation.CorrelationId = "ambient";
        var terminal = new RecordingPublishTerminal();

        await Run(terminal, new Dictionary<string, string> { [MessagingHeaders.CorrelationId] = "caller" });

        Assert.Equal("caller", SentHeaders(terminal)[MessagingHeaders.CorrelationId]);
    }

    [Fact]
    public async Task OnPublish_NoAmbientCorrelation_DoesNotAddHeader()
    {
        MessagingCorrelation.CorrelationId = null;
        var terminal = new RecordingPublishTerminal();

        await Run(terminal);

        Assert.DoesNotContain(MessagingHeaders.CorrelationId, SentHeaders(terminal).Keys);
    }

    [Fact]
    public async Task OnPublish_Success_LeavesStatusUnset()
    {
        using var recorder = new ActivityRecorder();

        var outcome = await Run(new RecordingPublishTerminal());

        Assert.True(outcome.IsSuccessful);
        Assert.Equal(ActivityStatusCode.Unset, recorder.Single(SendName).Status);
    }

    [Fact]
    public async Task OnPublish_FailedOutcome_SetsErrorStatusAndReturnsOutcome()
    {
        using var recorder = new ActivityRecorder();
        var terminal = new RecordingPublishTerminal { Result = new PublishOutcome(false, "broker down") };

        var outcome = await Run(terminal);

        var activity = recorder.Single(SendName);
        Assert.Equal(new PublishOutcome(false, "broker down"), outcome);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("broker down", activity.StatusDescription);
    }

    [Fact]
    public async Task OnPublish_TerminalThrows_RecordsExceptionAndRethrows()
    {
        using var recorder = new ActivityRecorder();
        var terminal = new RecordingPublishTerminal { ThrowOnSend = new InvalidOperationException("boom") };

        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(terminal));

        var activity = recorder.Single(SendName);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("boom", activity.StatusDescription);
        Assert.Contains(activity.Events, e => e.Name == "exception");
    }
}
