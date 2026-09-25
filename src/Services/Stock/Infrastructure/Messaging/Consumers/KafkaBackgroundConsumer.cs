using System.Diagnostics;
using Confluent.Kafka;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Observability;

namespace Stock.Infrastructure.Messaging.Consumers;

public abstract class KafkaBackgroundConsumer<TMessage>(
    IKafkaConsumerFactory consumerFactory,
    ILogger<KafkaBackgroundConsumer<TMessage>> logger,
    IDeadLetterPublisher dlq) : BackgroundService
    where TMessage : IExternalEvent
{
    private IConsumer<string, TMessage>? _consumer;
    private TopicPartitionOffset? _failingOffset;
    private int _failedAttempts;

    protected abstract string GroupId { get; }

    protected abstract string ClientId { get; }

    protected abstract string Topic { get; }

    protected virtual string DeadLetterSourceTopic => Topic;

    protected virtual int MaxHandleAttempts => 5;

    protected virtual bool HoldCommitWhenDeferred => false;

    protected abstract Task<bool> HandleAsync(TMessage message, Headers headers, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer = consumerFactory.Create<TMessage>(GroupId, Topic, ClientId);

        await Task.Run(() => ConsumeLoopAsync(stoppingToken), stoppingToken);
    }

    private async Task ConsumeLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            ConsumeResult<string, TMessage> result;

            try
            {
                result = _consumer!.Consume(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex) when (ex.Error.Code is ErrorCode.Local_ValueDeserialization
                                                  or ErrorCode.Local_KeyDeserialization
                                              && ex.ConsumerRecord is not null)
            {
                CorrelationContext.CorrelationId =
                    KafkaTelemetry.ExtractCorrelationId(ex.ConsumerRecord.Message.Headers);
                using var poisonActivity = KafkaTelemetry.StartProcess(ex.ConsumerRecord, GroupId);
                poisonActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                await MovePoisonToDeadLetterAsync(ex, ct);
                continue;
            }
            catch (ConsumeException ex)
            {
                if (ex.Error.IsFatal)
                {
                    logger.LogCritical(ex, "Fatal error while consuming a message from {Topic}", Topic);
                    throw;
                }

                logger.LogError(ex, "Failed to consume a message from {Topic}", Topic);
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
                continue;
            }

            var message = result.Message.Value;

            if (message is null)
            {
                StoreOffset(result.TopicPartitionOffset);
                continue;
            }

            CorrelationContext.CorrelationId = KafkaTelemetry.ExtractCorrelationId(result.Message.Headers);
            using var activity = KafkaTelemetry.StartProcess(result, GroupId);

            bool commit;

            try
            {
                commit = await HandleAsync(message, result.Message.Headers, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                activity?.AddException(ex);

                var attempt = RegisterFailure(result.TopicPartitionOffset);

                if (attempt < MaxHandleAttempts)
                {
                    logger.LogWarning(ex,
                        "Unhandled error while processing a message at {Offset} from {Topic}, attempt {Attempt}/{MaxAttempts}",
                        result.TopicPartitionOffset, Topic, attempt, MaxHandleAttempts);
                }
                else
                {
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                    if (await TryMoveToFatalAsync(message, ex, ct))
                    {
                        StoreOffset(result.TopicPartitionOffset);
                        continue;
                    }
                }

                Seek(result.TopicPartitionOffset);
                activity?.Dispose();
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
                continue;
            }

            if (!commit)
            {
                if (HoldCommitWhenDeferred) continue;

                Seek(result.TopicPartitionOffset);
                activity?.Dispose();
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
            else
            {
                StoreOffset(result.TopicPartitionOffset);
            }
        }
    }

    private async Task MovePoisonToDeadLetterAsync(ConsumeException ex, CancellationToken ct)
    {
        var record = ex.ConsumerRecord;
        logger.LogError(ex, "Message at {Offset} from {Topic} can't be deserialized, moving it to fatal DLQ",
            record.TopicPartitionOffset, Topic);

        try
        {
            await dlq.PublishPoisonAsync(DeadLetterSourceTopic, record.Message, ex, ct);
            StoreOffset(record.TopicPartitionOffset);
        }
        catch (Exception dlqEx) when (!ct.IsCancellationRequested)
        {
            logger.LogError(dlqEx, "Failed to move an undeserializable message from {Topic} to DLQ", Topic);
            Seek(record.TopicPartitionOffset);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }

    private async Task<bool> TryMoveToFatalAsync(TMessage message, Exception ex, CancellationToken ct)
    {
        logger.LogError(ex,
            "Message for aggregate {AggregateId} from {Topic} failed {Attempts} times, moving it to fatal DLQ",
            message.AggregateId, Topic, _failedAttempts);

        try
        {
            await dlq.PublishFatalAsync(DeadLetterSourceTopic, message, ex, _failedAttempts, ct);
            return true;
        }
        catch (Exception dlqEx) when (!ct.IsCancellationRequested)
        {
            logger.LogError(dlqEx, "Failed to move a message from {Topic} to fatal DLQ", Topic);
            return false;
        }
    }

    private int RegisterFailure(TopicPartitionOffset offset)
    {
        if (_failingOffset is null
            || _failingOffset.TopicPartition != offset.TopicPartition
            || _failingOffset.Offset != offset.Offset)
        {
            _failingOffset = offset;
            _failedAttempts = 0;
        }

        return ++_failedAttempts;
    }

    private void StoreOffset(TopicPartitionOffset handled)
    {
        _failingOffset = null;
        _failedAttempts = 0;

        try
        {
            _consumer!.StoreOffset(new TopicPartitionOffset(handled.TopicPartition, handled.Offset + 1));
        }
        catch (KafkaException ex)
        {
            logger.LogWarning(ex, "Failed to store an offset for {Topic}", Topic);
        }
    }

    private void Seek(TopicPartitionOffset offset)
    {
        try
        {
            _consumer!.Seek(offset);
        }
        catch (KafkaException ex)
        {
            logger.LogWarning(ex, "Failed to seek an offset for {Topic}", Topic);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        CloseConsumer();
    }

    public override void Dispose()
    {
        CloseConsumer();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private void CloseConsumer()
    {
        var consumer = Interlocked.Exchange(ref _consumer, null);
        if (consumer is null) return;

        try
        {
            consumer.Close();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to close the consumer for {Topic}", Topic);
        }
        finally
        {
            consumer.Dispose();
        }
    }
}