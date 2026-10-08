using Messaging.Middlewares;
using Messaging.Serializers;

namespace Messaging.IntegrationTests.Smoke;

[Collection(KafkaCollection.Name)]
public class StartupAndHappyPathTests(KafkaFixture kafka, ITestOutputHelper output)
{
    [Fact]
    public async Task Startup_CreatesMainAndDlqTopics()
    {
        var topic = TestNames.Unique("orders");
        var group = TestNames.Unique("billing");
        await using var host = await StartHost(topic, group, new ConsumerRecorder<OrderPlaced>());

        var topics = host.Kafka.ListTopics();

        Assert.Contains(topic, topics);
        Assert.Contains($"{group}.{topic}.dlq", topics);
        Assert.Equal("-1", await host.Kafka.GetTopicConfigAsync($"{group}.{topic}.dlq", "retention.ms"));
    }

    [Fact]
    public async Task Publish_ConsumerReceivesTypedMessageWithMessagingHeaders()
    {
        var topic = TestNames.Unique("orders");
        var group = TestNames.Unique("billing");
        var recorder = new ConsumerRecorder<OrderPlaced>();
        var capture = new DeliveryCaptureLog();
        await using var host = await StartHost(topic, group, recorder, capture);

        var outcome = await host.PublishAsync(new OrderPlaced("o-1"), topic, "key-1");

        Assert.True(outcome.IsSuccessful, outcome.ErrorMessage);
        await recorder.WaitForCallsAsync(1);
        Assert.Equal(new OrderPlaced("o-1"), Assert.Single(recorder.Calls));
        var context = Assert.Single(capture.For<OrderPlaced>());
        Assert.Equal(topic, context.Topic);
        Assert.Equal(group, context.ConsumerGroup);
        Assert.Equal(nameof(OrderPlaced), context.Headers[MessagingHeaders.EventType]);
        Assert.Equal(context.MessageId, context.Headers[MessagingHeaders.MessageId]);
    }
    
    private Task<MessagingTestHost> StartHost(string topic, string group, ConsumerRecorder<OrderPlaced> recorder,
        DeliveryCaptureLog? capture = null)
    {
        return MessagingTestHost.StartAsync(kafka, output,
            services =>
            {
                services.AddSingleton(recorder);
                services.AddSingleton(capture ?? new DeliveryCaptureLog());
            },
            messaging => messaging
                .AddMessage<OrderPlaced>(topic, new JsonDefaultSerializer())
                .AddConsumer<OrderPlaced, ScriptedConsumer<OrderPlaced>>(new ConsumerOptions(topic, group))
                .AddDeliveryMiddleware<CaptureDeliveryMiddleware>()
                .AddExceptionHandling());
    }
}
