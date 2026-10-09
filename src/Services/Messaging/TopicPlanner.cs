using Confluent.Kafka.Admin;

namespace Messaging;

internal static class TopicPlanner
{
    public static List<TopicSpecification> Plan(MessagingRegistry registry, MessagingOptions options)
    {
        var plan = new Dictionary<string, TopicSpecification>();

        foreach (var topic in registry.Messages.Values.Select(m => m.Topic))
            Add(topic, false);

        foreach (var (topic, group) in registry.Consumers.Keys)
        {
            Add(topic, false);
            Add(TopicNames.Dlq(group, topic), true);
        }

        foreach (var configured in options.Topics)
            Add(configured.Name, false);

        return [.. plan.Values];

        void Add(string name, bool retainForever)
        {
            if (plan.ContainsKey(name)) return;

            var configured = options.Topics.FirstOrDefault(t => t.Name == name);
            var specification = new TopicSpecification
            {
                Name = name,
                NumPartitions = configured.Name is null ? options.DefaultNumPartitions : configured.NumPartitions,
                ReplicationFactor = configured.Name is null
                    ? options.DefaultReplicationFactor
                    : configured.ReplicationFactor
            };

            if (retainForever)
                specification.Configs = new Dictionary<string, string> { ["retention.ms"] = "-1" };

            plan.Add(name, specification);
        }
    }
}
