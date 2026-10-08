using Messaging.Middlewares;
using Microsoft.Extensions.Logging;

namespace Messaging.Tests.Middlewares;

public class LoggingMiddlewareTests
{
    [Fact]
    public async Task OnDelivery_ReturnsOutcomeFromNextUnchanged()
    {
        var middleware = new LoggingMiddleware(new CapturingLogger<LoggingMiddleware>());
        var expected = new ConsumeOutcome(MessageConsumeResult.Retry, "later");

        var outcome = await middleware.OnDeliveryAsync(DeliveryContexts.Sample(),
            () => Task.FromResult(expected), CancellationToken.None);

        Assert.Equal(expected, outcome);
    }

    [Fact]
    public async Task OnDelivery_LogsBeforeAndAfterWithMessageIdTopicAndOutcome()
    {
        var logger = new CapturingLogger<LoggingMiddleware>();
        var middleware = new LoggingMiddleware(logger);

        await middleware.OnDeliveryAsync(DeliveryContexts.Sample(),
            () => Task.FromResult(new ConsumeOutcome(MessageConsumeResult.DeadLetter, "bad")),
            CancellationToken.None);

        Assert.Equal(2, logger.Entries.Count);
        Assert.All(logger.Entries, e => Assert.Equal(LogLevel.Debug, e.Level));
        Assert.Contains("message-id", logger.Entries[0].Message);
        Assert.Contains("topic", logger.Entries[0].Message);
        Assert.Contains("DeadLetter", logger.Entries[1].Message);
        Assert.Contains("bad", logger.Entries[1].Message);
    }

    [Fact]
    public async Task OnDelivery_NextThrows_ExceptionPropagatesAndAfterIsNotLogged()
    {
        var logger = new CapturingLogger<LoggingMiddleware>();
        var middleware = new LoggingMiddleware(logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.OnDeliveryAsync(
            DeliveryContexts.Sample(), () => throw new InvalidOperationException("boom"), CancellationToken.None));

        Assert.Single(logger.Entries);
    }

    [Fact]
    public void OnPublish_ReturnsNextAndLogsMessageIdAndTopic()
    {
        var logger = new CapturingLogger<LoggingMiddleware>();
        var middleware = new LoggingMiddleware(logger);
        PublishDelegate next = (_, _) => Task.FromResult(new PublishOutcome(true));

        var result = middleware.OnPublishAsync(PublishContexts.Sample(), next, CancellationToken.None);

        Assert.Same(next, result);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Contains("message-id", entry.Message);
        Assert.Contains(PublisherHarness.Topic, entry.Message);
    }
}
