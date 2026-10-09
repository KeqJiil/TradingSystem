using System.Text;
using Confluent.Kafka;
using Messaging.Abstractions;
using Messaging.Dlq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Messaging;

internal class BasicKafkaConsumer(
    IServiceProvider sp,
    MessagingRegistry registry,
    MessagingOptions options,
    ILogger<BasicKafkaConsumer> logger,
    IDeadLetterPublisher deadLetter,
    string clientId,
    string groupId,
    string topic) : BackgroundService
{
    private IConsumer<string, byte[]>? _kafkaConsumer;
    private readonly Dictionary<TopicPartition, (long Offset, int Attempts)> _failures = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        _kafkaConsumer ??= ConfigureConsumer();

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
                    logger.LogCritical(ex, "Fatal error while consuming a message from {Topic}", topic);
                    throw;
                }

                logger.LogError(ex, "Failed to consume a message from {Topic}", topic);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                continue;
            }

            var headers = new Dictionary<string, string>();
            foreach (var h in consumeMessage.Message.Headers)
                headers[h.Key] = Encoding.UTF8.GetString(h.GetValueBytes());

            headers.TryGetValue(MessagingHeaders.EventType, out var messageType);

            var rawMessage = new RawMessage(
                consumeMessage.Message.Value ?? Array.Empty<byte>(), consumeMessage.Topic, groupId,
                messageType ?? "Unknown", consumeMessage.Offset.Value, consumeMessage.Partition.Value,
                consumeMessage.Message.Timestamp.UtcDateTime, headers, consumeMessage.Message.Key);

            if (consumeMessage.Message.Value is null)
            {
                logger.LogWarning("Received a message without payload from {Topic} at {Offset}, moving it to the DLQ",
                    consumeMessage.Topic, consumeMessage.Offset.Value);
                await ParkAsync(consumeMessage, rawMessage, DlqReasons.EmptyPayload, "Message has no payload", 0,
                    stoppingToken);
                continue;
            }

            await using var scope = sp.CreateAsyncScope();

            if (messageType is null)
            {
                logger.LogWarning("Received message without {EventTypeHeader} header, moving it to the DLQ",
                    MessagingHeaders.EventType);
                await ParkAsync(consumeMessage, rawMessage, DlqReasons.UnknownType,
                    $"Missing {MessagingHeaders.EventType} header", 0, stoppingToken);
                continue;
            }

            if (!registry.Consumers.TryGetValue((consumeMessage.Topic, groupId), out var bindings)
                || !bindings.TryGetValue(messageType, out var consumerBinding))
            {
                logger.LogWarning(
                    "No consumer registered for message type {MessageType} from topic {Topic} in group {Group}, moving it to the DLQ",
                    messageType, consumeMessage.Topic, groupId);
                await ParkAsync(consumeMessage, rawMessage, DlqReasons.UnknownType,
                    $"No consumer registered for message type {messageType}", 0, stoppingToken);
                continue;
            }

            var result = await consumerBinding.Dispatch.Invoke(scope.ServiceProvider, rawMessage, stoppingToken);

            switch (result.Kind)
            {
                case MessageConsumeResult.Success:
                    StoreOffset(consumeMessage.TopicPartitionOffset);
                    break;
                case MessageConsumeResult.Retry:
                    var attempt = RegisterFailure(consumeMessage.TopicPartitionOffset);
                    var maxAttempts = consumerBinding.Options.MaxAttempts ?? options.MaxAttempts;
                    if (attempt < maxAttempts)
                    {
                        logger.LogWarning(
                            "Failed to process a message from {Topic} at {Offset}, attempt {Attempt}/{MaxAttempts}, retrying after {RetryDelay}",
                            consumeMessage.Topic, consumeMessage.Offset.Value, attempt, maxAttempts,
                            options.RetryDelay);

                        Seek(consumeMessage.TopicPartitionOffset);
                        await Task.Delay(options.RetryDelay, stoppingToken);
                    }
                    else
                    {
                        await ParkAsync(consumeMessage, rawMessage, DlqReasons.Exhausted, result.Reason, attempt,
                            stoppingToken);
                    }

                    break;
                case MessageConsumeResult.DeadLetter:
                    await ParkAsync(consumeMessage, rawMessage, DlqReasons.DeadLetter, result.Reason,
                        _failures.TryGetValue(consumeMessage.TopicPartition, out var failure) ? failure.Attempts : 0,
                        stoppingToken);
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

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        CloseConsumer();
    }

    private async Task ParkAsync(ConsumeResult<string, byte[]> consumed, RawMessage raw, string reason,
        string? details, int attempt, CancellationToken ct)
    {
        var parked = await deadLetter.PublishAsync(raw, reason, details, attempt, ct);

        if (parked)
        {
            StoreOffset(consumed.TopicPartitionOffset);
            return;
        }

        logger.LogError(
            "Failed to publish a message from {Topic} at {Offset} to the DLQ, it will be read again",
            consumed.Topic, consumed.Offset.Value);
        Seek(consumed.TopicPartitionOffset);
        await Task.Delay(TimeSpan.FromSeconds(5), ct);
    }

    private void CloseConsumer()
    {
        var consumer = Interlocked.Exchange(ref _kafkaConsumer, null);
        if (consumer is null) return;

        try
        {
            consumer.Close();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to close the consumer for {Topic}", topic);
        }
        finally
        {
            consumer.Dispose();
        }
    }

    private void StoreOffset(TopicPartitionOffset handled)
    {
        _failures.Remove(handled.TopicPartition);

        try
        {
            var next = new TopicPartitionOffset(handled.TopicPartition, handled.Offset + 1);
            _kafkaConsumer?.StoreOffset(next);
            if (!options.UseAutoCommit) _kafkaConsumer?.Commit([next]);
        }
        catch (KafkaException ex)
        {
            logger.LogWarning(ex, "Failed to store an offset for {Topic}", topic);
        }
    }

    private void Seek(TopicPartitionOffset offset)
    {
        try
        {
            _kafkaConsumer?.Seek(offset);
        }
        catch (KafkaException ex)
        {
            logger.LogWarning(ex, "Failed to seek an offset for {Topic}", topic);
        }
    }

    private int RegisterFailure(TopicPartitionOffset offset)
    {
        var attempts = _failures.TryGetValue(offset.TopicPartition, out var failure) && failure.Offset == offset.Offset
            ? failure.Attempts + 1
            : 1;
        _failures[offset.TopicPartition] = (offset.Offset, attempts);
        return attempts;
    }

    private IConsumer<string, byte[]> ConfigureConsumer()
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
            .SetOffsetsCommittedHandler((_, committed) =>
                KafkaClientLogging.LogCommitted(logger, clientId, committed))
            .Build();

        builder.Subscribe(topic);

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
    Dictionary<string, string> Headers,
    string? Key = null);