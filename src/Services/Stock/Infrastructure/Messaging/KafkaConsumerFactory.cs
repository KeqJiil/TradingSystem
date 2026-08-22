using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Stock.Presentation.Options;

namespace Stock.Infrastructure.Messaging;

public interface IKafkaConsumerFactory
{
    IConsumer<string, string> Create(string groupId, string clientId);
}

public class KafkaConsumerFactory(IOptions<KafkaOptions> options) : IKafkaConsumerFactory
{
    public IConsumer<string, string> Create(string groupId, string clientId) =>
        new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            GroupId = groupId,
            ClientId = clientId,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            GroupProtocol = GroupProtocol.Consumer,
        }).Build();
}