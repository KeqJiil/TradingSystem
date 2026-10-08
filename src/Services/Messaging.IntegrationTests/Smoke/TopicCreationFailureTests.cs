using Confluent.Kafka.Admin;
using Messaging.Serializers;

namespace Messaging.IntegrationTests.Smoke;

[Collection(KafkaCollection.Name)]
public class TopicCreationFailureTests(KafkaFixture kafka, ITestOutputHelper output)
{
    [Fact]
    public async Task Startup_TopicCannotBeCreated_HostFailsToStartInsteadOfContinuing()
    {
        var topic = TestNames.Unique("orders");

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => MessagingTestHost.StartAsync(kafka, output,
            _ => { },
            messaging => messaging.AddMessage<OrderPlaced>(topic, new JsonDefaultSerializer()),
            options => options.DefaultReplicationFactor = 3));

        Assert.True(exception is CreateTopicsException
                    || exception is AggregateException { InnerExceptions: var inner }
                    && inner.OfType<CreateTopicsException>().Any(),
            exception.ToString());
    }

    [Fact]
    public async Task Startup_TopicAlreadyExists_HostStartsNormally()
    {
        var topic = TestNames.Unique("orders");
        var group = TestNames.Unique("billing");
        await using var first = await OrdersHost.StartAsync(kafka, output, topic, group,
            new ConsumerRecorder<OrderPlaced>());

        await using var second = await OrdersHost.StartAsync(kafka, output, topic, group,
            new ConsumerRecorder<OrderPlaced>());

        Assert.Contains(topic, second.Kafka.ListTopics());
    }
}
