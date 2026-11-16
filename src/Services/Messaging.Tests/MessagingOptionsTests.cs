namespace Messaging.Tests;

public class MessagingOptionsTests
{
    [Fact]
    public void Validate_DefaultRetryDelays_DoesNotThrow()
    {
        new MessagingOptions { BootstrapServers = "localhost:9092" }.Validate();
    }

    [Fact]
    public void Validate_MaxRetryDelayEqualToRetryDelay_DoesNotThrow()
    {
        new MessagingOptions
        {
            BootstrapServers = "localhost:9092",
            RetryDelay = TimeSpan.FromSeconds(5),
            MaxRetryDelay = TimeSpan.FromSeconds(5)
        }.Validate();
    }

    [Fact]
    public void Validate_MaxRetryDelayBelowRetryDelay_Throws()
    {
        var options = new MessagingOptions
        {
            BootstrapServers = "localhost:9092",
            RetryDelay = TimeSpan.FromSeconds(5),
            MaxRetryDelay = TimeSpan.FromSeconds(1)
        };

        var ex = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains(nameof(MessagingOptions.MaxRetryDelay), ex.Message);
    }
}
