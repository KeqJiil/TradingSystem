using System.Text;

namespace Messaging.IntegrationTests.Smoke;

[Collection(KafkaCollection.Name)]
public class DeadLetterTests(KafkaFixture kafka, ITestOutputHelper output)
{
    private static readonly OrderPlaced Valid = new("o-valid");

    [Fact]
    public async Task DeadLetterOutcome_ParksMessageWithReasonAndContinuesWithNext()
    {
        var topic = TestNames.Unique("orders");
        var group = TestNames.Unique("billing");
        var rejected = new OrderPlaced("o-rejected");
        var recorder = new ConsumerRecorder<OrderPlaced>
        {
            Behavior = (message, _) => message == rejected
                ? new ConsumeOutcome(MessageConsumeResult.DeadLetter, "bad order")
                : new ConsumeOutcome(MessageConsumeResult.Success)
        };
        await using var host = await OrdersHost.StartAsync(kafka, output, topic, group, recorder);

        await host.PublishAsync(rejected, topic, "key-1");
        await host.PublishAsync(Valid, topic, "key-2");

        var parked = Assert.Single(await host.Kafka.ReadAsync(OrdersHost.DlqTopic(topic, group), 1));
        await Wait.UntilAsync(() => recorder.CallsFor(Valid) == 1, "the next message to be processed");
        Assert.Equal(1, recorder.CallsFor(rejected));
        Assert.Equal("key-1", parked.Key);
        Assert.Contains("o-rejected", parked.Text);
        Assert.Equal("dead-letter", parked.Headers[MessagingHeaders.DlqReason]);
        Assert.Equal("bad order", parked.Headers[MessagingHeaders.ExceptionMessage]);
        Assert.Equal(topic, parked.Headers[MessagingHeaders.OriginalTopic]);
    }

    [Fact]
    public async Task UndeserializablePayload_ParksRawBytesWithoutCallingConsumer()
    {
        var topic = TestNames.Unique("orders");
        var group = TestNames.Unique("billing");
        var recorder = new ConsumerRecorder<OrderPlaced>();
        var payload = Encoding.UTF8.GetBytes("not json {{");
        await using var host = await OrdersHost.StartAsync(kafka, output, topic, group, recorder);

        await host.Kafka.ProduceAsync(topic, "key-1", payload,
            new Dictionary<string, string> { [MessagingHeaders.EventType] = nameof(OrderPlaced) });
        await host.PublishAsync(Valid, topic, "key-2");

        var parked = Assert.Single(await host.Kafka.ReadAsync(OrdersHost.DlqTopic(topic, group), 1));
        await Wait.UntilAsync(() => recorder.CallsFor(Valid) == 1, "the next message to be processed");
        Assert.Equal([Valid], recorder.Calls);
        Assert.Equal("key-1", parked.Key);
        Assert.Equal(payload, parked.Value);
        Assert.Equal("dead-letter", parked.Headers[MessagingHeaders.DlqReason]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("SomethingElse")]
    public async Task UnknownOrMissingEventType_ParksMessageAsUnknownType(string? eventType)
    {
        var topic = TestNames.Unique("orders");
        var group = TestNames.Unique("billing");
        var recorder = new ConsumerRecorder<OrderPlaced>();
        var payload = Encoding.UTF8.GetBytes("""{"OrderId":"o-1"}""");
        var headers = eventType is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { [MessagingHeaders.EventType] = eventType };
        await using var host = await OrdersHost.StartAsync(kafka, output, topic, group, recorder);

        await host.Kafka.ProduceAsync(topic, "key-1", payload, headers);
        await host.PublishAsync(Valid, topic, "key-2");

        var parked = Assert.Single(await host.Kafka.ReadAsync(OrdersHost.DlqTopic(topic, group), 1));
        await Wait.UntilAsync(() => recorder.CallsFor(Valid) == 1, "the next message to be processed");
        Assert.Equal([Valid], recorder.Calls);
        Assert.Equal("key-1", parked.Key);
        Assert.Equal(payload, parked.Value);
        Assert.Equal("unknown-type", parked.Headers[MessagingHeaders.DlqReason]);
    }
}
