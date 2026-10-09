using System.Diagnostics;
using Messaging.Serializers;

namespace Messaging.IntegrationTests.Smoke;

[Collection(KafkaCollection.Name)]
public class PublisherFailureTests(KafkaFixture kafka, ITestOutputHelper output)
{
    [Fact]
    public async Task Publish_BrokerUnavailable_FailsWithinMessageTimeoutInsteadOfHanging()
    {
        var topic = TestNames.Unique("orders");
        await using var host = await MessagingTestHost.StartAsync(kafka, output,
            _ => { },
            messaging => messaging.AddMessage<OrderPlaced>(topic, new JsonDefaultSerializer()),
            options =>
            {
                options.BootstrapServers = "127.0.0.1:1";
                options.EnableTopicRegistration = false;
                options.MessageTimeoutMs = 2_000;
            });
        var stopwatch = Stopwatch.StartNew();

        var outcome = await host.PublishAsync(new OrderPlaced("o-1"), topic, "key-1");

        Assert.False(outcome.IsSuccessful);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ErrorMessage));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"publish took {stopwatch.Elapsed}");
    }
}
