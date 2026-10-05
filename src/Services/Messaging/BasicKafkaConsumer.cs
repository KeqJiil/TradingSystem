using System.Text;
using Confluent.Kafka;
using Messaging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Messaging;

internal class BasicKafkaConsumer : BackgroundService
{
    private IConsumer<string, byte[]> _kafkaConsumer;
    private TopicPartitionOffset? _failingOffset;
    private int _failedAttempts;

    private readonly IServiceProvider _sp;
    private readonly MessagingRegistry _registry;
    private readonly MessagingOptions _options;
    private readonly ILogger<BasicKafkaConsumer> _logger;

    private readonly string _clientId;
    private readonly string _groupId;
    private readonly string _topic;

    public BasicKafkaConsumer(IServiceProvider sp, MessagingRegistry registry, MessagingOptions options,
        ILogger<BasicKafkaConsumer> logger, string clientId, string groupId, string topic)
    {
        _sp = sp;
        _registry = registry;
        _options = options;
        _logger = logger;
        _clientId = clientId;
        _groupId = groupId;
        _topic = topic;
        _kafkaConsumer = ConfigureConsumer();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, byte[]> consumeMessage;

            try
            {
                consumeMessage = _kafkaConsumer.Consume(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                if (ex.Error.IsFatal)
                {
                    _logger.LogCritical(ex, "Fatal error while consuming a message from {Topic}", _topic);
                    throw;
                }

                _logger.LogError(ex, "Failed to consume a message from {Topic}", _topic);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                continue;
            }

            using var scope = _sp.CreateScope();

            var headers = new Dictionary<string, string>();
            foreach (var h in consumeMessage.Message.Headers)
                headers[h.Key] = Encoding.UTF8.GetString(h.GetValueBytes());

            headers.TryGetValue(MessagingHeaders.EventType, out var messageType);

            if (messageType is null)
            {
                _logger.LogWarning("Received message without {EventTypeHeader} header, skipping",
                    MessagingHeaders.EventType);
                MovePoisonedMessage(new RawMessage(consumeMessage.Message.Value, consumeMessage.Topic, _groupId,
                    "Unknown", consumeMessage.Offset.Value,
                    consumeMessage.Partition.Value, consumeMessage.Message.Timestamp.UtcDateTime, headers));
                StoreOffset(consumeMessage.TopicPartitionOffset);
                continue;
            }

            var rawMessage = new RawMessage(
                consumeMessage.Message.Value, consumeMessage.Topic, _groupId, messageType, consumeMessage.Offset.Value,
                consumeMessage.Partition.Value, consumeMessage.Message.Timestamp.UtcDateTime, headers);

            if (!_registry.Consumers.TryGetValue((consumeMessage.Topic, _groupId), out var bindings)
                || !bindings.TryGetValue(messageType, out var consumerBinding))
            {
                _logger.LogWarning(
                    "No consumer registered for message type {MessageType} from topic {Topic} in group {Group}, skipping",
                    messageType, consumeMessage.Topic, _groupId);
                MovePoisonedMessage(rawMessage);
                StoreOffset(consumeMessage.TopicPartitionOffset);
                continue;
            }

            var result = await consumerBinding.Dispatch.Invoke(scope.ServiceProvider, rawMessage, stoppingToken);

            switch (result.Kind)
            {
                case MessageConsumeResult.Success:
                    StoreOffset(consumeMessage.TopicPartitionOffset);
                    break;
                case MessageConsumeResult.Retry:
                    break;
                case MessageConsumeResult.DeadLetter:
                    MoveToDeadLetterQueue(rawMessage);
                    break;
            }
        }
    }

    public override void Dispose()
    {
        CloseConsumer();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private void MoveToDeadLetterQueue(RawMessage message)
    {
        throw new NotImplementedException();
    }

    private void MovePoisonedMessage(RawMessage message)
    {
        throw new NotImplementedException();
    }

    private void CloseConsumer()
    {
        if (_kafkaConsumer is null) return;
        var consumer = Interlocked.Exchange(ref _kafkaConsumer, null);

        try
        {
            consumer.Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to close the consumer for {Topic}", _topic);
        }
        finally
        {
            consumer.Dispose();
        }
    }

    private void StoreOffset(TopicPartitionOffset handled)
    {
        _failingOffset = null;
        _failedAttempts = 0;

        try
        {
            _kafkaConsumer.StoreOffset(new TopicPartitionOffset(handled.TopicPartition, handled.Offset + 1));
        }
        catch (KafkaException ex)
        {
            _logger.LogWarning(ex, "Failed to store an offset for {Topic}", _topic);
        }
    }

    private void Seek(TopicPartitionOffset offset)
    {
        try
        {
            _kafkaConsumer.Seek(offset);
        }
        catch (KafkaException ex)
        {
            _logger.LogWarning(ex, "Failed to seek an offset for {Topic}", _topic);
        }
    }

    private IConsumer<string, byte[]> ConfigureConsumer()
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = _options.UseAutoCommit,
            EnableAutoOffsetStore = false,
            ClientId = _clientId,
            AutoCommitIntervalMs = _options.AutoCommitIntervalMs
        };

        var builder = new ConsumerBuilder<string, byte[]>(config)
            .SetErrorHandler((_, error) => KafkaClientLogging.LogError(_logger, _clientId, error))
            .SetLogHandler((_, message) => KafkaClientLogging.LogMessage(_logger, message))
            .SetOffsetsCommittedHandler((_, committed) =>
                KafkaClientLogging.LogCommitted(_logger, _clientId, committed))
            .Build();

        builder.Subscribe(_topic);

        return builder;
    }
}

internal record RawMessage(
    byte[] Payload,
    string Topic,
    string ConsumerGroup,
    string MessageType,
    long Offset,
    int Partition,
    DateTimeOffset Timestamp,
    Dictionary<string, string> Headers);