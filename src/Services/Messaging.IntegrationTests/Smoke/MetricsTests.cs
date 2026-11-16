using MetricRecorder = Messaging.Testing.MetricRecorder;

namespace Messaging.IntegrationTests.Smoke;

[Collection(KafkaCollection.Name)]
public class MetricsTests(KafkaFixture kafka, ITestOutputHelper output)
{
    private const string Duration = "messaging.process.duration";
    private const string Retries = "messaging.consumer.retries";
    private const string DeadLetters = "messaging.consumer.dlq";

    private static readonly OrderPlaced First = new("o-1");

    [Fact]
    public async Task Process_Success_RecordsDurationWithSuccessOutcome()
    {
        using var metrics = new MetricRecorder();
        var topic = TestNames.Unique("orders");
        var group = TestNames.Unique("billing");
        var recorder = new ConsumerRecorder<OrderPlaced>();
        await using var host = await OrdersHost.StartAsync(kafka, output, topic, group, recorder);

        await host.PublishAsync(First, "key-1");

        await Wait.UntilAsync(() => metrics.For(Duration, topic).Count == 1, "duration recorded");
        var duration = Assert.Single(metrics.For(Duration, topic));
        Assert.True(duration.Value >= 0);
        Assert.Equal(group, duration.Tags["messaging.consumer.group.name"]);
        Assert.Equal(nameof(OrderPlaced), duration.Tags["messaging.message.type"]);
        Assert.Equal("Success", duration.Tags["messaging.consume.outcome"]);
        Assert.Empty(metrics.For(Retries, topic));
        Assert.Empty(metrics.For(DeadLetters, topic));
    }

    [Fact]
    public async Task Retry_ThenSuccess_CountsEachRetryAndRecordsEachAttempt()
    {
        using var metrics = new MetricRecorder();
        var topic = TestNames.Unique("orders");
        var group = TestNames.Unique("billing");
        var recorder = new ConsumerRecorder<OrderPlaced>
        {
            Behavior = (_, attempt) => new ConsumeOutcome(
                attempt < 3 ? MessageConsumeResult.Retry : MessageConsumeResult.Success, "not yet")
        };
        await using var host = await OrdersHost.StartAsync(kafka, output, topic, group, recorder);

        await host.PublishAsync(First, "key-1");

        await Wait.UntilAsync(() => metrics.For(Duration, topic).Count == 3, "three attempts recorded");
        Assert.Equal(["Retry", "Retry", "Success"],
            metrics.For(Duration, topic).Select(m => m.Tags["messaging.consume.outcome"]));
        var retries = metrics.For(Retries, topic);
        Assert.Equal(2, retries.Sum(m => m.Value));
        Assert.All(retries, m => Assert.Equal(group, m.Tags["messaging.consumer.group.name"]));
        Assert.Empty(metrics.For(DeadLetters, topic));
    }

    [Fact]
    public async Task Retry_Exhausted_CountsRetriesAndDeadLetterWithExhaustedReason()
    {
        using var metrics = new MetricRecorder();
        var topic = TestNames.Unique("orders");
        var group = TestNames.Unique("billing");
        var recorder = new ConsumerRecorder<OrderPlaced>
        {
            Behavior = (_, _) => new ConsumeOutcome(MessageConsumeResult.Retry, "still failing")
        };
        await using var host = await OrdersHost.StartAsync(kafka, output, topic, group, recorder,
            options => options.MaxAttempts = 3);

        await host.PublishAsync(First, "key-1");

        await Wait.UntilAsync(() => metrics.For(DeadLetters, topic).Count == 1, "dead letter counted");
        var deadLetter = Assert.Single(metrics.For(DeadLetters, topic));
        Assert.Equal(1, deadLetter.Value);
        Assert.Equal("exhausted", deadLetter.Tags["messaging.dlq.reason"]);
        Assert.Equal(group, deadLetter.Tags["messaging.consumer.group.name"]);
        Assert.Equal(2, metrics.For(Retries, topic).Sum(m => m.Value));
    }

    [Fact]
    public async Task DeadLetterOutcome_CountsDeadLetterWithoutRetries()
    {
        using var metrics = new MetricRecorder();
        var topic = TestNames.Unique("orders");
        var group = TestNames.Unique("billing");
        var recorder = new ConsumerRecorder<OrderPlaced>
        {
            Behavior = (_, _) => new ConsumeOutcome(MessageConsumeResult.DeadLetter, "bad order")
        };
        await using var host = await OrdersHost.StartAsync(kafka, output, topic, group, recorder);

        await host.PublishAsync(First, "key-1");

        await Wait.UntilAsync(() => metrics.For(DeadLetters, topic).Count == 1, "dead letter counted");
        Assert.Equal("dead-letter", Assert.Single(metrics.For(DeadLetters, topic)).Tags["messaging.dlq.reason"]);
        Assert.Empty(metrics.For(Retries, topic));
        Assert.Equal("DeadLetter",
            Assert.Single(metrics.For(Duration, topic)).Tags["messaging.consume.outcome"]);
    }
}
