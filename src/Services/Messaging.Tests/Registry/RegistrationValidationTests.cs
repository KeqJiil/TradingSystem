using Messaging.Abstractions;
using Messaging.Serializers;
using Messaging.Tests.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Messaging.Tests.Registry;

public class RegistrationValidationTests
{
    private sealed class TestMessageHandler : IMessageConsumer<TestMessage>
    {
        public Task<ConsumeOutcome> ConsumeAsync(TestMessage message, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success));
        }
    }

    private sealed class TestMessageHandler2 : IMessageConsumer<TestMessage2>
    {
        public Task<ConsumeOutcome> ConsumeAsync(TestMessage2 message, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ConsumeOutcome(MessageConsumeResult.Success));
        }
    }

    private record TestMessage(string Value);

    private record TestMessage2(string Value);

    [Fact]
    public void AddMessage_RegistersTopicAndSerializerByType()
    {
        var serializer = new JsonDefaultSerializer();
        var services = new ServiceCollection();

        services.AddMessaging(
            b => b.AddMessage<TestMessage>("topic", serializer),
            o => o.BootstrapServers = "localhost:9092");

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<MessagingRegistry>();

        var info = registry.Messages[typeof(TestMessage)];
        Assert.Equal("topic", info.Topic);
        Assert.Same(serializer, info.Serializer);
    }

    [Fact]
    public void AddMessage_SameTypeTwice_Throws()
    {
        var serializer = new JsonDefaultSerializer();
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddMessaging(
                b => b.AddMessage<TestMessage>("topic", serializer).AddMessage<TestMessage>("topic", serializer),
                o => o.BootstrapServers = "localhost:9092"));
    }

    [Fact]
    public void AddConsumer_WithoutAddMessage_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<KeyNotFoundException>(() =>
            services.AddMessaging(
                b => b.AddConsumer<TestMessage, TestMessageHandler>(new ConsumerOptions("topic", "group")),
                o => o.BootstrapServers = "localhost:9092"));
    }

    [Fact]
    public void AddConsumer_TwoMessageTypesSameTopicAndGroup_BothBound()
    {
        var serializer = new JsonDefaultSerializer();
        var services = new ServiceCollection();

        services.AddMessaging(
            b => b.AddMessage<TestMessage>("topic", serializer)
                .AddMessage<TestMessage2>("topic", serializer)
                .AddConsumer<TestMessage, TestMessageHandler>(new ConsumerOptions("topic", "group"))
                .AddConsumer<TestMessage2, TestMessageHandler2>(new ConsumerOptions("topic", "group")),
            o => o.BootstrapServers = "localhost:9092");

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<MessagingRegistry>();

        Assert.Equal(2, registry.Consumers[("topic", "group")].Count);
    }

    [Fact]
    public void AddConsumer_SameMessageTypeSameTopicAndGroupTwice_Throws()
    {
        var serializer = new JsonDefaultSerializer();
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddMessaging(
                b => b.AddMessage<TestMessage>("topic", serializer)
                    .AddConsumer<TestMessage, TestMessageHandler>(new ConsumerOptions("topic", "group"))
                    .AddConsumer<TestMessage, TestMessageHandler>(new ConsumerOptions("topic", "group")),
                o => o.BootstrapServers = "localhost:9092"));
    }

    [Fact]
    public void AddConsumer_RegistersConsumerAsScoped()
    {
        var serializer = new JsonDefaultSerializer();
        var services = new ServiceCollection();

        services.AddMessaging(
            b => b.AddMessage<TestMessage>("topic", serializer)
                .AddConsumer<TestMessage, TestMessageHandler>(new ConsumerOptions("topic", "group")),
            o => o.BootstrapServers = "localhost:9092");

        using var provider = services.BuildServiceProvider();
        var scope1 = provider.CreateScope();
        var scope2 = provider.CreateScope();

        var consumer1 = scope1.ServiceProvider.GetRequiredService<TestMessageHandler>();
        var consumer2 = scope2.ServiceProvider.GetRequiredService<TestMessageHandler>();

        Assert.NotSame(consumer1, consumer2);
    }

    [Fact]
    public void AddMessaging_RegistersOneHostedConsumerPerTopicAndGroup()
    {
        var services = new ServiceCollection();
        services.AddMessaging(
            b => b.AddMessage<TestMessage>("topic", new JsonDefaultSerializer())
                .AddConsumer<TestMessage, TestMessageHandler>(new ConsumerOptions("topic", "group")),
            o => o.BootstrapServers = "localhost:9092");
    }

    [Fact]
    public void AddMessaging_RegistersTopicRegistrationBeforeConsumers()
    {
        var services = new ServiceCollection();

        services.AddMessaging(
            b => b.AddMessage<TestMessage>("topic", new JsonDefaultSerializer())
                .AddConsumer<TestMessage, TestMessageHandler>(new ConsumerOptions("topic", "group")),
            o => o.BootstrapServers = "localhost:9092"
            );

        var hosted = services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();

        Assert.Equal(2, hosted.Count);
        Assert.Equal(typeof(TopicRegistrationService), hosted[0].ImplementationType);
        Assert.NotNull(hosted[1].ImplementationFactory);
    }

    [Fact]
    public void AddDeliveryMiddleware_RegistersKeyedScopedInCallOrder()
    {
        var services = new ServiceCollection();
        var log = new TraceLog();
        services.AddSingleton(log);
        services.AddMessaging(
            b => b.AddMessage<TestMessage>("topic", new JsonDefaultSerializer())
                .AddConsumer<TestMessage, TestMessageHandler>(new ConsumerOptions("topic", "group"))
                .AddDeliveryMiddleware<ProbeA>()
                .AddDeliveryMiddleware<ProbeB>(),
            o => o.BootstrapServers = "localhost:9092");

        using var provider = services.BuildServiceProvider();
        var middlewares = provider.GetKeyedServices<IMessageDeliveryMiddleware>("MessagingDeliveryMiddleware").ToList();

        Assert.Equal(2, middlewares.Count);
        Assert.Equal(typeof(ProbeA), middlewares[0].GetType());
        Assert.Equal(typeof(ProbeB), middlewares[1].GetType());
    }

    [Fact]
    public void AddMessaging_RegistersPublisherAsScopedAndProducerAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddMessaging(
            b => b.AddMessage<TestMessage>("topic", new JsonDefaultSerializer()),
            o => o.BootstrapServers = "localhost:9092");

        using var provider = services.BuildServiceProvider();
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var publisher1 = scope1.ServiceProvider.GetRequiredService<IMessagePublisher>();
        var publisher2 = scope2.ServiceProvider.GetRequiredService<IMessagePublisher>();

        var producer1 = scope1.ServiceProvider.GetRequiredService<BasicKafkaProducer>();
        var producer2 = scope2.ServiceProvider.GetRequiredService<BasicKafkaProducer>();

        Assert.NotSame(publisher1, publisher2);
        Assert.Same(producer1, producer2);
    }
}