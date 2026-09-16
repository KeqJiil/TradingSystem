using Confluent.Kafka;

namespace Stock.Infrastructure.Messaging.Consumers;

public abstract class KafkaBackgroundConsumer<TMessage>(
    IKafkaConsumerFactory consumerFactory,
    ILogger logger) : BackgroundService
{
    private IConsumer<string, TMessage>? _consumer;

    protected abstract string GroupId { get; }

    protected abstract string ClientId { get; }

    protected abstract string Topic { get; }

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
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Failed to consume a message from {Topic}", Topic);
                continue;
            }

            var message = result.Message.Value;

            if (message is null)
            {
                Commit(result);
                continue;
            }

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
                logger.LogError(ex, "Unhandled error while processing a message from {Topic}", Topic);
                continue;
            }

            if (commit) Commit(result);
        }
    }

    private void Commit(ConsumeResult<string, TMessage> result)
    {
        try
        {
            _consumer!.Commit(result);
        }
        catch (KafkaException ex)
        {
            logger.LogWarning(ex, "Failed to commit an offset for {Topic}", Topic);
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