using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Messaging;

internal class KafkaTopicRegister(
    IOptions<MessagingOptions> options,
    ILogger<KafkaTopicRegister> logger,
    IAdminClient adminClient)
{
    private readonly MessagingOptions _messagingOptions = options.Value;

    public async Task RegisterTopicsAsync(CancellationToken ct)
    {
        var topicList = _messagingOptions.Topics.Select(t => new TopicSpecification
        {
            Name = t.Name,
            NumPartitions = t.NumPartitions,
            ReplicationFactor = t.ReplicationFactor
        }).ToList();

        try
        {
            await adminClient.CreateTopicsAsync(topicList);

            logger.LogInformation("Topics {Topic} created successfully", string.Join(", ", topicList.Select(t => t.Name)));
        }
        catch (CreateTopicsException e) when (e.Results.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            logger.LogWarning("Topic {Topic} already exists",
                e.Results.Where(r => r.Error.Code == ErrorCode.TopicAlreadyExists).Select(r => r.Topic));
        }
    }
}