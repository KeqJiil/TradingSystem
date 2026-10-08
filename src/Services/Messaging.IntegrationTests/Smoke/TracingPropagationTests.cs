using System.Diagnostics;
using Messaging.Middlewares;

namespace Messaging.IntegrationTests.Smoke;

[Collection(KafkaCollection.Name)]
public class TracingPropagationTests(KafkaFixture kafka, ITestOutputHelper output)
{
    private const string AmbientSource = "it-ambient";

    [Fact]
    public async Task Publish_InsideTrace_TraceAndCorrelationReachConsumerThroughKafkaHeaders()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is AmbientSource or MessagingTelemetry.SourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        var topic = TestNames.Unique("orders");
        var group = TestNames.Unique("billing");
        var seen = new TaskCompletionSource<(ActivityTraceId? TraceId, string? CorrelationId)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var recorder = new ConsumerRecorder<OrderPlaced>
        {
            Behavior = (_, _) =>
            {
                seen.TrySetResult((Activity.Current?.TraceId, MessagingCorrelation.CorrelationId));
                return new ConsumeOutcome(MessageConsumeResult.Success);
            }
        };
        await using var host = await OrdersHost.StartAsync(kafka, output, topic, group, recorder,
            configureMessaging: messaging => messaging.AddTracing());
        using var ambientSource = new ActivitySource(AmbientSource);
        using var ambient = ambientSource.StartActivity("ambient")!;
        MessagingCorrelation.CorrelationId = "corr-1";

        await host.PublishAsync(new OrderPlaced("o-1"), topic, "key-1");

        var (traceId, correlationId) = await seen.Task.WaitAsync(Wait.Default);
        Assert.Equal(ambient.TraceId, traceId);
        Assert.Equal("corr-1", correlationId);
        var onWire = Assert.Single(await host.Kafka.ReadAsync(topic, 1));
        Assert.StartsWith($"00-{ambient.TraceId.ToHexString()}-", onWire.Headers[MessagingHeaders.TraceParent]);
        Assert.Equal("corr-1", onWire.Headers[MessagingHeaders.CorrelationId]);
    }

    [Fact]
    public async Task Publish_WithoutTracingMiddleware_AddsNoTraceHeaders()
    {
        var topic = TestNames.Unique("orders");
        var group = TestNames.Unique("billing");
        await using var host = await OrdersHost.StartAsync(kafka, output, topic, group,
            new ConsumerRecorder<OrderPlaced>());
        MessagingCorrelation.CorrelationId = "corr-1";

        await host.PublishAsync(new OrderPlaced("o-1"), topic, "key-1");

        var onWire = Assert.Single(await host.Kafka.ReadAsync(topic, 1));
        Assert.DoesNotContain(MessagingHeaders.TraceParent, onWire.Headers.Keys);
        Assert.DoesNotContain(MessagingHeaders.CorrelationId, onWire.Headers.Keys);
    }
}
