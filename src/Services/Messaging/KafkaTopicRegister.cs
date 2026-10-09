using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Messaging;

internal class KafkaTopicRegister(
    IOptions<MessagingOptions> options,
    MessagingRegistry registry,
    ILogger<KafkaTopicRegister> logger,
    IAdminClient adminClient)
{
    private readonly MessagingOptions _messagingOptions = options.Value;

    public async Task RegisterTopicsAsync(CancellationToken ct)
    {
        var topicList = TopicPlanner.Plan(registry, _messagingOptions);

        if (topicList.Count == 0) return;

        try
        {
            await adminClient.CreateTopicsAsync(topicList);

            logger.LogInformation("Topics {Topic} created successfully", string.Join(", ", topicList.Select(t => t.Name)));
        }
        catch (CreateTopicsException e)
        {
            if (e.Results.Any(r => r.Error.IsError && r.Error.Code != ErrorCode.TopicAlreadyExists))
            {
                logger.LogCritical(e, "Failed to create topics {Topic}",
                    string.Join(", ", e.Results.Where(r => r.Error.IsError && r.Error.Code != ErrorCode.TopicAlreadyExists)
                        .Select(r => $"{r.Topic}: {r.Error.Reason}")));
                throw;
            }

            logger.LogInformation("Topics {Topic} already exist",
                string.Join(", ", e.Results.Where(r => r.Error.Code == ErrorCode.TopicAlreadyExists).Select(r => r.Topic)));
        }
    }
}
