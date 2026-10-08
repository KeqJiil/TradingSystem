namespace Messaging.IntegrationTests.Smoke;

[Collection(KafkaCollection.Name)]
public class MultiPartitionRetryTests(KafkaFixture kafka, ITestOutputHelper output)
{
    private const int MaxAttempts = 3;
    private const int HealthyCount = 30;

    private static readonly OrderPlaced Poison = new("poison");

    [Fact]
    public async Task Retry_WhileOtherPartitionsKeepSucceeding_StillExhaustsAfterMaxAttempts()
    {
        var topic = TestNames.Unique("orders");
        var group = TestNames.Unique("billing");
        var gate = new TaskCompletionSource();
        var capture = new DeliveryCaptureLog();
        var recorder = new ConsumerRecorder<OrderPlaced>();
        recorder.Behavior = (message, _) => message == Poison
            ? new ConsumeOutcome(MessageConsumeResult.Retry, "poison")
            : new ConsumeOutcome(MessageConsumeResult.Success);
        recorder.Before = message => message == Poison && recorder.CallsFor(Poison) == 1
            ? gate.Task
            : Task.CompletedTask;
        await using var host = await OrdersHost.StartAsync(kafka, output, topic, group, recorder, options =>
        {
            options.MaxAttempts = MaxAttempts;
            options.DefaultNumPartitions = 3;
        }, capture);

        await host.PublishAsync(Poison, topic, "poison-key");
        await recorder.WaitForCallsAsync(1);
        for (var i = 0; i < HealthyCount; i++)
            await host.PublishAsync(new OrderPlaced($"h-{i}"), topic, $"key-{i}");
        await Task.Delay(TimeSpan.FromSeconds(2));
        gate.SetResult();

        var parked = Assert.Single(await host.Kafka.ReadAsync(OrdersHost.DlqTopic(topic, group), 1));
        await Wait.UntilAsync(() => recorder.Calls.Count(c => c != Poison) >= HealthyCount,
            "all healthy messages to be processed");
        var healthyPartitions = capture.For<OrderPlaced>()
            .Where(c => c.Message != Poison).Select(c => c.Partition).Distinct().Count();
        Assert.True(healthyPartitions >= 2, $"healthy messages landed on {healthyPartitions} partition(s)");
        Assert.Equal(MaxAttempts, recorder.CallsFor(Poison));
        Assert.Equal("exhausted", parked.Headers[MessagingHeaders.DlqReason]);
        Assert.Equal(MaxAttempts.ToString(), parked.Headers[MessagingHeaders.Attempt]);
    }
}
