using Messaging.Serializers;

namespace Messaging.Tests.Registry;

public class SerializerResolverTests
{
    [Fact]
    public void For_ReturnsSerializerRegisteredForMessage()
    {
        var serializer = new JsonDefaultSerializer();
        var services = new ServiceCollection();
        services.AddMessaging(
            b => b.AddMessage<TestMessage>("topic", serializer),
            o => o.BootstrapServers = "localhost:9092");

        using var provider = services.BuildServiceProvider();

        Assert.Same(serializer, provider.GetRequiredService<IMessageSerializerResolver>().For<TestMessage>());
    }

    [Fact]
    public void For_UnregisteredMessage_Throws()
    {
        var services = new ServiceCollection();
        services.AddMessaging(
            b => b.AddMessage<TestMessage>("topic", new JsonDefaultSerializer()),
            o => o.BootstrapServers = "localhost:9092");

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IMessageSerializerResolver>().For<TestMessage2>());
    }
}
