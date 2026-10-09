namespace Messaging.IntegrationTests.Smoke;

[Collection(KafkaCollection.Name)]
public class OffsetCommitTests(KafkaFixture kafka, ITestOutputHelper output)
{
    [Fact]
    public async Task Restart_WithSameGroup_DoesNotRedeliverHandledMessage()
    {
        var topic = TestNames.Unique("orders");
        var group = TestNames.Unique("billing");
        var first = new ConsumerRecorder<OrderPlaced>();
        var second = new ConsumerRecorder<OrderPlaced>();

        await using (var host = await OrdersHost.StartAsync(kafka, output, topic, group, first))
        {
            await host.PublishAsync(new OrderPlaced("o-1"), topic, "key-1");
            await first.WaitForCallsAsync(1);
        }

        await using (var host = await OrdersHost.StartAsync(kafka, output, topic, group, second))
        {
            await host.PublishAsync(new OrderPlaced("o-2"), topic, "key-2");
            await second.WaitForCallsAsync(1);
        }

        Assert.Equal([new OrderPlaced("o-2")], second.Calls);
    }
}