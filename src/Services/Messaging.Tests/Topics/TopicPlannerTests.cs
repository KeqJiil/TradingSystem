using Confluent.Kafka.Admin;
using Messaging.Serializers;

namespace Messaging.Tests.Topics;

public class TopicPlannerTests
{
    [Fact]
    public void Plan_MessageTopic_IsPlannedWithDefaults()
    {
        var plan = Plan(b => b.AddMessage<TestMessage>("orders", new JsonDefaultSerializer()));

        var topic = Find(plan, "orders");
        Assert.Equal(1, topic.NumPartitions);
        Assert.Equal(1, topic.ReplicationFactor);
        Assert.Null(topic.Configs);
    }

    [Fact]
    public void Plan_Consumer_AddsDlqTopicWithInfiniteRetention()
    {
        var plan = Plan(b => b.AddMessage<TestMessage>("orders", new JsonDefaultSerializer())
            .AddConsumer<TestMessage, TestMessageHandler>(new ConsumerOptions("orders", "billing")));

        var dlq = Find(plan, "billing.orders.dlq");
        Assert.Equal("-1", dlq.Configs["retention.ms"]);
        Assert.Equal(2, plan.Count);
    }

    [Fact]
    public void Plan_TwoMessageTypesSameTopicAndGroup_PlansOneDlq()
    {
        var serializer = new JsonDefaultSerializer();

        var plan = Plan(b => b.AddMessage<TestMessage>("orders", serializer)
            .AddMessage<TestMessage2>("orders", serializer)
            .AddConsumer<TestMessage, TestMessageHandler>(new ConsumerOptions("orders", "billing"))
            .AddConsumer<TestMessage2, TestMessageHandler2>(new ConsumerOptions("orders", "billing")));

        Assert.Equal(["billing.orders.dlq", "orders"], plan.Select(t => t.Name).Order());
    }

    [Fact]
    public void Plan_TwoGroupsOnSameTopic_PlanDlqPerGroup()
    {
        var serializer = new JsonDefaultSerializer();

        var plan = Plan(b => b.AddMessage<TestMessage>("orders", serializer)
            .AddConsumer<TestMessage, TestMessageHandler>(new ConsumerOptions("orders", "billing"))
            .AddConsumer<TestMessage, TestMessageHandler>(new ConsumerOptions("orders", "shipping")));

        Assert.Equal(["billing.orders.dlq", "orders", "shipping.orders.dlq"], plan.Select(t => t.Name).Order());
    }

    [Fact]
    public void Plan_ConsumedTopicWithoutProducer_IsStillPlanned()
    {
        var plan = Plan(b => b.AddMessage<TestMessage>("orders", new JsonDefaultSerializer())
            .AddConsumer<TestMessage, TestMessageHandler>(new ConsumerOptions("external", "billing")));

        Assert.Contains(plan, t => t.Name == "external");
        Assert.Contains(plan, t => t.Name == "billing.external.dlq");
    }

    [Fact]
    public void Plan_ConfiguredTopic_OverridesPartitionsAndReplication()
    {
        var plan = Plan(
            b => b.AddMessage<TestMessage>("orders", new JsonDefaultSerializer()),
            o => o.Topics = [new TopicData("orders", 3, 12)]);

        var topic = Find(plan, "orders");
        Assert.Equal(12, topic.NumPartitions);
        Assert.Equal(3, topic.ReplicationFactor);
    }

    [Fact]
    public void Plan_ConfiguredDlqTopic_KeepsRetentionAndTakesOverrides()
    {
        var plan = Plan(
            b => b.AddMessage<TestMessage>("orders", new JsonDefaultSerializer())
                .AddConsumer<TestMessage, TestMessageHandler>(new ConsumerOptions("orders", "billing")),
            o => o.Topics = [new TopicData("billing.orders.dlq", 3, 4)]);

        var dlq = Find(plan, "billing.orders.dlq");
        Assert.Equal(4, dlq.NumPartitions);
        Assert.Equal("-1", dlq.Configs["retention.ms"]);
    }

    [Fact]
    public void Plan_ConfiguredTopicNotInRegistry_IsStillPlanned()
    {
        var plan = Plan(
            b => b.AddMessage<TestMessage>("orders", new JsonDefaultSerializer()),
            o => o.Topics = [new TopicData("manual", 1, 2)]);

        Assert.Equal(2, Find(plan, "manual").NumPartitions);
    }

    [Fact]
    public void Plan_CustomDefaults_AreApplied()
    {
        var plan = Plan(
            b => b.AddMessage<TestMessage>("orders", new JsonDefaultSerializer()),
            o =>
            {
                o.DefaultNumPartitions = 6;
                o.DefaultReplicationFactor = 3;
            });

        var topic = Find(plan, "orders");
        Assert.Equal(6, topic.NumPartitions);
        Assert.Equal(3, topic.ReplicationFactor);
    }

    [Fact]
    public void Plan_NothingRegistered_IsEmpty()
    {
        Assert.Empty(Plan(_ => { }));
    }
    
    private static List<TopicSpecification> Plan(Action<MessagingBuilder> configure,
        Action<MessagingOptions>? configureOptions = null)
    {
        var services = new ServiceCollection();
        var options = new MessagingOptions { BootstrapServers = "localhost:9092" };
        configureOptions?.Invoke(options);

        services.AddMessaging(configure, o =>
        {
            o.BootstrapServers = options.BootstrapServers;
            o.Topics = options.Topics;
            o.DefaultNumPartitions = options.DefaultNumPartitions;
            o.DefaultReplicationFactor = options.DefaultReplicationFactor;
        });

        using var provider = services.BuildServiceProvider();

        return TopicPlanner.Plan(provider.GetRequiredService<MessagingRegistry>(), options);
    }

    private static TopicSpecification Find(List<TopicSpecification> plan, string name)
    {
        return plan.Single(t => t.Name == name);
    }
}
