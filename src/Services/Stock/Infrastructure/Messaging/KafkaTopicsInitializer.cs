using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Options;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging;

public class  KafkaTopicsInitializer(
    IOptions<KafkaOptions> kafkaOptions,
    IOptions<DeadLetterOptions> deadLetterOptions,
    ILogger<KafkaTopicsInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var suffix = deadLetterOptions.Value.TopicSuffix;

        var topics = TopicNames.All
            .SelectMany(topic => new[] { topic, topic + suffix + ".retry", topic + suffix + ".fatal" })
            .Append(deadLetterOptions.Value.UnknownTopic)
            .ToArray();

        using var admin = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = kafkaOptions.Value.BootstrapServers
        }).Build();

        try
        {
            await admin.CreateTopicsAsync(topics.Select(topic => new TopicSpecification
            {
                Name = topic,
                NumPartitions = 1,
                ReplicationFactor = 1
            }));

            logger.LogInformation("Created {Count} Kafka topics", topics.Length);
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r =>
                                                   r.Error.Code is ErrorCode.TopicAlreadyExists or ErrorCode.NoError))
        {
            logger.LogInformation("Kafka topics already exist");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}