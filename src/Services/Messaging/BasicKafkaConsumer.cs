using System.Text;
using Confluent.Kafka;
using Messaging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Messaging;

internal class BasicKafkaConsumer(IServiceProvider sp, MessagingRegistry registry, MessagingOptions options, ILogger<BasicKafkaConsumer> logger, string clientId, string groupId, string topic) : BackgroundService
{
    private IConsumer<string, byte[]> _kafkaConsumer;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ConfigureConsumer();
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = sp.CreateScope();
            
            var consumeMessage = _kafkaConsumer.Consume(stoppingToken);
            
            var headers = new Dictionary<string, string>();
            foreach (var h in consumeMessage.Message.Headers)
                headers[h.Key] = Encoding.UTF8.GetString(h.GetValueBytes());  

            headers.TryGetValue(MessagingHeaders.EventType, out var messageType);
            if (messageType is null)
            {
                logger.LogWarning("Received message without {EventTypeHeader} header, skipping", MessagingHeaders.EventType);
                throw new InvalidOperationException($"Received message without {MessagingHeaders.EventType} header");
            }
            
            var rawMessage = new RawMessage(
                consumeMessage.Message.Value, consumeMessage.Topic, groupId, messageType, consumeMessage.Offset.Value, 
                consumeMessage.Partition.Value, consumeMessage.Message.Timestamp.UtcDateTime, headers);
            
            var consumerBinding = registry.Consumers[(consumeMessage.Topic, groupId)][messageType];
            
            var result = await consumerBinding.Dispatch.Invoke(scope.ServiceProvider, rawMessage, stoppingToken);
        }
    }
    
    private void ConfigureConsumer()
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = options.UseAutoCommit,
            EnableAutoOffsetStore = false,
            ClientId = clientId,
            AutoCommitIntervalMs = options.AutoCommitIntervalMs
        };
        
        var builder = new ConsumerBuilder<string, byte[]>(config)
            .SetErrorHandler((_, error) => KafkaClientLogging.LogError(logger, clientId, error))
            .SetLogHandler((_, message) => KafkaClientLogging.LogMessage(logger, message))
            .SetOffsetsCommittedHandler((_, committed) => KafkaClientLogging.LogCommitted(logger, clientId, committed))
            .Build();
        
        builder.Subscribe(topic);
        
        _kafkaConsumer = builder;
    }
}

internal record RawMessage(byte[] Payload, string Topic, string ConsumerGroup, string MessageType, long Offset, int Partition, DateTimeOffset Timestamp, Dictionary<string, string> Headers);