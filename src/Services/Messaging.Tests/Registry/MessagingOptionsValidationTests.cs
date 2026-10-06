namespace Messaging.Tests.Registry;

public class MessagingOptionsValidationTests
{
    private static void Register(Action<MessagingOptions> configure)
    {
        new ServiceCollection().AddMessaging(_ => { }, configure);
    }

    [Fact]
    public void AddMessaging_ValidOptions_DoesNotThrow()
    {
        Register(o => o.BootstrapServers = "localhost:9092");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void AddMessaging_EmptyBootstrapServers_Throws(string bootstrapServers)
    {
        Assert.Throws<InvalidOperationException>(() => Register(o => o.BootstrapServers = bootstrapServers));
    }

    [Fact]
    public void AddMessaging_ZeroMaxAttempts_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Register(o =>
        {
            o.BootstrapServers = "localhost:9092";
            o.MaxAttempts = 0;
        }));
    }

    [Fact]
    public void AddMessaging_NegativeRetryDelay_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Register(o =>
        {
            o.BootstrapServers = "localhost:9092";
            o.RetryDelay = TimeSpan.FromSeconds(-1);
        }));
    }

    [Fact]
    public void AddMessaging_ZeroDefaultPartitions_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Register(o =>
        {
            o.BootstrapServers = "localhost:9092";
            o.DefaultNumPartitions = 0;
        }));
    }

    [Fact]
    public void AddMessaging_ZeroDefaultReplicationFactor_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Register(o =>
        {
            o.BootstrapServers = "localhost:9092";
            o.DefaultReplicationFactor = 0;
        }));
    }

    [Fact]
    public void AddMessaging_NewOptions_AreCopiedIntoRegisteredOptions()
    {
        var services = new ServiceCollection();
        services.AddMessaging(_ => { }, o =>
        {
            o.BootstrapServers = "localhost:9092";
            o.MaxAttempts = 9;
            o.RetryDelay = TimeSpan.FromSeconds(7);
            o.DefaultNumPartitions = 4;
            o.DefaultReplicationFactor = 2;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MessagingOptions>>().Value;

        Assert.Equal(9, options.MaxAttempts);
        Assert.Equal(TimeSpan.FromSeconds(7), options.RetryDelay);
        Assert.Equal(4, options.DefaultNumPartitions);
        Assert.Equal(2, options.DefaultReplicationFactor);
    }
}
