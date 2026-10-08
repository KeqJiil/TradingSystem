using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Messaging.Tests.Publisher;

public class DefaultPublishTerminalTests
{
    [Fact]
    public async Task PublishAsync_SerializationFails_ReturnsFailedOutcomeWithoutReachingKafka()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddMessaging(
            b => b.AddMessage<SampleMessage>("topic", new FailingSerializer()),
            o =>
            {
                o.BootstrapServers = "127.0.0.1:1";
                o.MessageTimeoutMs = 10_000;
            });
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
        var stopwatch = Stopwatch.StartNew();

        var outcome = await publisher.PublishAsync(new SampleMessage("name", 1),
            new PublishOptions("key", "topic", new Dictionary<string, string>()), CancellationToken.None);

        Assert.False(outcome.IsSuccessful);
        Assert.Contains("serialize", outcome.ErrorMessage);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"publish took {stopwatch.Elapsed}");
    }
}
