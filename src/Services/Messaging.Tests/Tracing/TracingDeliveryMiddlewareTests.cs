using System.Diagnostics;
using Messaging.Middlewares;

namespace Messaging.Tests.Tracing;

[Collection("Tracing")]
public class TracingDeliveryMiddlewareTests
{
    private static readonly ConsumeOutcome Success = new(MessageConsumeResult.Success);

    private static async Task<ConsumeOutcome> Run(DeliveryContext<TestMessage> context, DeliveryDelegate terminal)
    {
        var services = new ServiceCollection();
        new MessagingBuilder(services).AddDeliveryMiddleware<TracingDeliveryMiddleware>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        return await MessagingPipeline.RunDelivery(context, scope.ServiceProvider, terminal, CancellationToken.None);
    }

    private static DeliveryContext<TestMessage> WithHeaders(params (string Name, string Value)[] headers)
    {
        return DeliveryContexts.Sample() with
        {
            Partition = 3,
            Offset = 42,
            Headers = headers.ToDictionary(h => h.Name, h => h.Value)
        };
    }

    private static string TraceParent(ActivityTraceId traceId, ActivitySpanId spanId)
    {
        return $"00-{traceId.ToHexString()}-{spanId.ToHexString()}-01";
    }

    [Fact]
    public async Task OnDelivery_StartsConsumerActivityWithMessagingTags()
    {
        using var recorder = new ActivityRecorder();

        await Run(WithHeaders(), () => Task.FromResult(Success));

        var activity = recorder.Single("process topic");
        Assert.Equal(ActivityKind.Consumer, activity.Kind);
        Assert.Equal("kafka", activity.GetTagItem("messaging.system"));
        Assert.Equal("topic", activity.GetTagItem("messaging.destination.name"));
        Assert.Equal("group", activity.GetTagItem("messaging.consumer.group.name"));
        Assert.Equal("3", activity.GetTagItem("messaging.destination.partition.id"));
        Assert.Equal(42L, activity.GetTagItem("messaging.kafka.offset"));
        Assert.Equal("message-id", activity.GetTagItem("messaging.message.id"));
        Assert.Equal(nameof(TestMessage), activity.GetTagItem("messaging.message.type"));
    }

    [Fact]
    public async Task OnDelivery_WithTraceParent_ContinuesRemoteTrace()
    {
        using var recorder = new ActivityRecorder();
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();

        await Run(WithHeaders((MessagingHeaders.TraceParent, TraceParent(traceId, spanId))),
            () => Task.FromResult(Success));

        var activity = recorder.Single("process topic");
        Assert.Equal(traceId, activity.TraceId);
        Assert.Equal(spanId, activity.ParentSpanId);
    }

    [Fact]
    public async Task OnDelivery_WithTraceState_PassesItToActivity()
    {
        using var recorder = new ActivityRecorder();
        var traceParent = TraceParent(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom());

        await Run(WithHeaders((MessagingHeaders.TraceParent, traceParent), (MessagingHeaders.TraceState, "k=v")),
            () => Task.FromResult(Success));

        Assert.Equal("k=v", recorder.Single("process topic").TraceStateString);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("garbage")]
    public async Task OnDelivery_MissingOrInvalidTraceParent_StartsNewTrace(string? traceParent)
    {
        using var recorder = new ActivityRecorder();
        var context = traceParent is null
            ? WithHeaders()
            : WithHeaders((MessagingHeaders.TraceParent, traceParent));

        await Run(context, () => Task.FromResult(Success));

        var activity = recorder.Single("process topic");
        Assert.Null(activity.ParentId);
        Assert.Equal(default, activity.ParentSpanId);
    }

    [Fact]
    public async Task OnDelivery_ConsumerRunsInsideProcessActivity()
    {
        using var recorder = new ActivityRecorder();
        string? current = null;

        await Run(WithHeaders(), () =>
        {
            current = Activity.Current?.DisplayName;
            return Task.FromResult(Success);
        });

        Assert.Equal("process topic", current);
    }

    [Fact]
    public async Task OnDelivery_CorrelationHeader_IsVisibleToConsumerAndOnActivity()
    {
        using var recorder = new ActivityRecorder();
        string? seen = null;

        await Run(WithHeaders((MessagingHeaders.CorrelationId, "corr-1")), () =>
        {
            seen = MessagingCorrelation.CorrelationId;
            return Task.FromResult(Success);
        });

        Assert.Equal("corr-1", seen);
        Assert.Equal("corr-1", recorder.Single("process topic").GetTagItem("correlation.id"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task OnDelivery_MissingOrBlankCorrelation_GeneratesOne(string? header)
    {
        using var recorder = new ActivityRecorder();
        string? seen = null;
        var context = header is null ? WithHeaders() : WithHeaders((MessagingHeaders.CorrelationId, header));

        await Run(context, () =>
        {
            seen = MessagingCorrelation.CorrelationId;
            return Task.FromResult(Success);
        });

        Assert.True(Guid.TryParse(seen, out _));
        Assert.Equal(seen, recorder.Single("process topic").GetTagItem("correlation.id"));
    }

    [Fact]
    public async Task OnDelivery_NoListener_StillSetsCorrelationAndReturnsOutcome()
    {
        string? seen = null;
        var expected = new ConsumeOutcome(MessageConsumeResult.Retry, "later");

        var outcome = await Run(WithHeaders((MessagingHeaders.CorrelationId, "corr-1")), () =>
        {
            seen = MessagingCorrelation.CorrelationId;
            return Task.FromResult(expected);
        });

        Assert.Equal("corr-1", seen);
        Assert.Equal(expected, outcome);
    }

    [Fact]
    public async Task OnDelivery_Success_LeavesStatusUnsetAndTagsOutcome()
    {
        using var recorder = new ActivityRecorder();

        var outcome = await Run(WithHeaders(), () => Task.FromResult(Success));

        var activity = recorder.Single("process topic");
        Assert.Equal(Success, outcome);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Equal("Success", activity.GetTagItem("messaging.consume.outcome"));
    }

    [Theory]
    [InlineData(MessageConsumeResult.Retry)]
    [InlineData(MessageConsumeResult.DeadLetter)]
    public async Task OnDelivery_NonSuccessOutcome_SetsErrorStatusWithReason(MessageConsumeResult kind)
    {
        using var recorder = new ActivityRecorder();
        var expected = new ConsumeOutcome(kind, "boom");

        var outcome = await Run(WithHeaders(), () => Task.FromResult(expected));

        var activity = recorder.Single("process topic");
        Assert.Equal(expected, outcome);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("boom", activity.StatusDescription);
        Assert.Equal(kind.ToString(), activity.GetTagItem("messaging.consume.outcome"));
    }

    [Fact]
    public async Task OnDelivery_ConsumerThrows_RecordsExceptionAndRethrows()
    {
        using var recorder = new ActivityRecorder();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Run(WithHeaders(), () => throw new InvalidOperationException("boom")));

        var activity = recorder.Single("process topic");
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("boom", activity.StatusDescription);
        Assert.Contains(activity.Events, e => e.Name == "exception");
    }

    [Fact]
    public async Task AddTracing_RegistersDeliveryAndPublishMiddlewares()
    {
        var services = new ServiceCollection();
        new MessagingBuilder(services).AddTracing();
        await using var provider = services.BuildServiceProvider();

        Assert.IsType<TracingDeliveryMiddleware>(Assert.Single(
            provider.GetKeyedServices<IMessageDeliveryMiddleware>("MessagingDeliveryMiddleware")));
        Assert.IsType<TracingPublishMiddleware>(Assert.Single(
            provider.GetKeyedServices<IMessagePublishMiddleware>("MessagingPublishMiddleware")));
    }

    [Fact]
    public async Task PublishInsideConsumer_ContinuesTraceAndCorrelationOfDeliveredMessage()
    {
        using var recorder = new ActivityRecorder();
        var terminal = new RecordingPublishTerminal();
        var traceId = ActivityTraceId.CreateRandom();
        var remoteSpanId = ActivitySpanId.CreateRandom();
        var services = new ServiceCollection();
        new MessagingBuilder(services).AddTracing();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = WithHeaders(
            (MessagingHeaders.TraceParent, TraceParent(traceId, remoteSpanId)),
            (MessagingHeaders.CorrelationId, "corr-1"));

        await MessagingPipeline.RunDelivery(context, scope.ServiceProvider, async () =>
        {
            await MessagingPublishPipeline.RunPublish(PublishContexts.Sample(), scope.ServiceProvider, terminal,
                CancellationToken.None);
            return Success;
        }, CancellationToken.None);

        var headers = Assert.Single(terminal.Calls).Headers;
        var process = recorder.Single("process topic");
        var send = recorder.Single($"send {PublisherHarness.Topic}");
        Assert.Equal("corr-1", headers[MessagingHeaders.CorrelationId]);
        Assert.Equal(TraceParent(traceId, send.SpanId), headers[MessagingHeaders.TraceParent]);
        Assert.Equal(process.SpanId, send.ParentSpanId);
        Assert.Equal(remoteSpanId, process.ParentSpanId);
    }
}
