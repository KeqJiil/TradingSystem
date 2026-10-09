using Messaging.Middlewares;

namespace Messaging.IntegrationTests.Smoke;

[Collection(KafkaCollection.Name)]
public class ConsumerExceptionTests(KafkaFixture kafka, ITestOutputHelper output)
{
    private static readonly OrderPlaced Failing = new("o-failing");
    private static readonly OrderPlaced Healthy = new("o-healthy");

    [Fact]
    public async Task ConsumerThrowsEveryTime_IsRetriedThenParkedAsExhaustedAndNextMessageContinues()
    {
        var topic = TestNames.Unique("orders");
        var group = TestNames.Unique("billing");
        var recorder = new ConsumerRecorder<OrderPlaced>
        {
            Behavior = (message, _) => message == Failing
                ? throw new InvalidOperationException("boom")
                : new ConsumeOutcome(MessageConsumeResult.Success)
        };
        await using var host = await StartHost(topic, group, recorder);

        await host.PublishAsync(Failing, topic, "key-1");
        await host.PublishAsync(Healthy, topic, "key-2");

        var parked = Assert.Single(await host.Kafka.ReadAsync(OrdersHost.DlqTopic(topic, group), 1));
        await Wait.UntilAsync(() => recorder.CallsFor(Healthy) == 1, "the next message to be processed");
        Assert.Equal(3, recorder.CallsFor(Failing));
        Assert.Equal("key-1", parked.Key);
        Assert.Contains("o-failing", parked.Text);
        Assert.Equal("exhausted", parked.Headers[MessagingHeaders.DlqReason]);
        Assert.Equal("3", parked.Headers[MessagingHeaders.Attempt]);
        Assert.Equal("boom", parked.Headers[MessagingHeaders.ExceptionMessage]);
    }

    [Fact]
    public async Task ConsumerThrowsOnce_IsRetriedAndSucceedsWithoutDlq()
    {
        var topic = TestNames.Unique("orders");
        var group = TestNames.Unique("billing");
        var recorder = new ConsumerRecorder<OrderPlaced>
        {
            Behavior = (_, attempt) => attempt == 1
                ? throw new InvalidOperationException("transient")
                : new ConsumeOutcome(MessageConsumeResult.Success)
        };
        await using var host = await StartHost(topic, group, recorder);

        await host.PublishAsync(Failing, topic, "key-1");
        await recorder.WaitForCallsAsync(2);

        var parked = await host.Kafka.ReadAsync(OrdersHost.DlqTopic(topic, group), 1, TimeSpan.FromSeconds(3));
        Assert.Empty(parked);
        Assert.Equal(2, recorder.CallCount);
    }
    
    private Task<MessagingTestHost> StartHost(string topic, string group, ConsumerRecorder<OrderPlaced> recorder)
    {
        return OrdersHost.StartAsync(kafka, output, topic, group, recorder,
            options => options.MaxAttempts = 3,
            configureMessaging: messaging => messaging.AddExceptionHandling());
    }
}
