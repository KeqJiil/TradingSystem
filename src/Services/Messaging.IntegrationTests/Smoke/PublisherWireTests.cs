using Messaging.Serializers;

namespace Messaging.IntegrationTests.Smoke;

[Collection(KafkaCollection.Name)]
public class PublisherWireTests(KafkaFixture kafka, ITestOutputHelper output)
{
    [Fact]
    public async Task Publish_WritesKeyPayloadAndHeadersToTopic()
    {
        var topic = TestNames.Unique("orders");
        await using var host = await StartHost(topic);

        var outcome = await host.PublishAsync(new OrderPlaced("o-1"), topic, "key-1",
            new Dictionary<string, string> { ["x-custom"] = "kept" });

        Assert.True(outcome.IsSuccessful, outcome.ErrorMessage);
        var message = Assert.Single(await host.Kafka.ReadAsync(topic, 1));
        Assert.Equal("key-1", message.Key);
        Assert.Equal("""{"OrderId":"o-1"}""", message.Text);
        Assert.Equal(1, message.HeaderCount(MessagingHeaders.EventType));
        Assert.Equal(nameof(OrderPlaced), message.Headers[MessagingHeaders.EventType]);
        Assert.Equal(1, message.HeaderCount(MessagingHeaders.MessageId));
        Assert.True(Guid.TryParse(message.Headers[MessagingHeaders.MessageId], out _));
        Assert.Equal("kept", message.Headers["x-custom"]);
    }

    [Fact]
    public async Task Publish_EachMessageGetsItsOwnMessageId()
    {
        var topic = TestNames.Unique("orders");
        await using var host = await StartHost(topic);

        await host.PublishAsync(new OrderPlaced("o-1"), topic, "key-1");
        await host.PublishAsync(new OrderPlaced("o-2"), topic, "key-2");

        var messages = await host.Kafka.ReadAsync(topic, 2);
        Assert.Equal(2, messages.Count);
        Assert.NotEqual(messages[0].Headers[MessagingHeaders.MessageId], messages[1].Headers[MessagingHeaders.MessageId]);
    }
    
    private Task<MessagingTestHost> StartHost(string topic)
    {
        return MessagingTestHost.StartAsync(kafka, output,
            _ => { },
            messaging => messaging.AddMessage<OrderPlaced>(topic, new JsonDefaultSerializer()));
    }
}
