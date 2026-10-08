namespace Messaging.IntegrationTests.Smoke;

[Collection(KafkaCollection.Name)]
public class RetryTests(KafkaFixture kafka, ITestOutputHelper output)
{
    private static readonly OrderPlaced First = new("o-1");
    private static readonly OrderPlaced Second = new("o-2");

    [Fact]
    public async Task Retry_ThenSuccess_ProcessesSameMessageAgainWithoutDlq()
    {
        var topic = TestNames.Unique("orders");
        var group = TestNames.Unique("billing");
        var recorder = new ConsumerRecorder<OrderPlaced>
        {
            Behavior = (_, attempt) => new ConsumeOutcome(
                attempt < 3 ? MessageConsumeResult.Retry : MessageConsumeResult.Success, "not yet")
        };
        await using var host = await OrdersHost.StartAsync(kafka, output, topic, group, recorder);

        await host.PublishAsync(First, topic, "key-1");
        await recorder.WaitForCallsAsync(3);

        var parked = await host.Kafka.ReadAsync(OrdersHost.DlqTopic(topic, group), 1, TimeSpan.FromSeconds(3));
        Assert.Empty(parked);
        Assert.Equal(3, recorder.CallsFor(First));
        Assert.Equal(3, recorder.CallCount);
    }

    [Fact]
    public async Task Retry_Exhausted_ParksMessageInDlqAndContinuesWithNext()
    {
        var topic = TestNames.Unique("orders");
        var group = TestNames.Unique("billing");
        var recorder = new ConsumerRecorder<OrderPlaced>
        {
            Behavior = (message, _) => message == First
                ? new ConsumeOutcome(MessageConsumeResult.Retry, "still failing")
                : new ConsumeOutcome(MessageConsumeResult.Success)
        };
        await using var host = await OrdersHost.StartAsync(kafka, output, topic, group, recorder,
            options => options.MaxAttempts = 3);

        await host.PublishAsync(First, topic, "key-1");
        await host.PublishAsync(Second, topic, "key-2");

        var parked = Assert.Single(await host.Kafka.ReadAsync(OrdersHost.DlqTopic(topic, group), 1));
        await Wait.UntilAsync(() => recorder.CallsFor(Second) == 1, "the next message to be processed");
        Assert.Equal(3, recorder.CallsFor(First));
        Assert.Equal("key-1", parked.Key);
        Assert.Contains("o-1", parked.Text);
        Assert.Equal("exhausted", parked.Headers[MessagingHeaders.DlqReason]);
        Assert.Equal("3", parked.Headers[MessagingHeaders.Attempt]);
        Assert.Equal("still failing", parked.Headers[MessagingHeaders.ExceptionMessage]);
        Assert.Equal(topic, parked.Headers[MessagingHeaders.OriginalTopic]);
        Assert.Equal(nameof(OrderPlaced), parked.Headers[MessagingHeaders.EventType]);
        Assert.True(parked.Headers.ContainsKey(MessagingHeaders.MessageId));
        Assert.True(parked.Headers.ContainsKey(MessagingHeaders.FirstFailureAt));
    }
}
