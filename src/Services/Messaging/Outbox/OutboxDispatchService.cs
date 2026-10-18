using Confluent.Kafka;
using Messaging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Messaging.Outbox;

internal class OutboxDispatchService : BackgroundService
{
    private static readonly HashSet<ErrorCode> PoisonCodes =
    [
        ErrorCode.MsgSizeTooLarge,
        ErrorCode.RecordListTooLarge,
        ErrorCode.InvalidMsg,
        ErrorCode.InvalidRecord
    ];

    private readonly ILogger<OutboxDispatchService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BasicKafkaProducer _producer;
    private readonly OutboxRelayOptions _options;

    public OutboxDispatchService(
        ILogger<OutboxDispatchService> logger,
        IServiceScopeFactory scopeFactory,
        BasicKafkaProducer producer,
        IOptions<OutboxRelayOptions> options,
        IOptions<MessagingOptions> messagingOptions)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _producer = producer;
        _options = options.Value;

        if (TimeSpan.FromMinutes(_options.LeaseMinutes) <= TimeSpan.FromMilliseconds(messagingOptions.Value.MessageTimeoutMs))
            throw new InvalidOperationException(
                $"{nameof(OutboxRelayOptions)}.{nameof(OutboxRelayOptions.LeaseMinutes)} must exceed {nameof(MessagingOptions)}.{nameof(MessagingOptions.MessageTimeoutMs)}.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.PollInterval);
        do
        {
            try
            {
                while (await DispatchBatchAsync(stoppingToken))
                {
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "An unexpected error occurred while dispatching outbox messages");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task<bool> DispatchBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

        var batch = await store.GetPendingAsync(_options.BatchSize, _options.LeaseMinutes, ct);
        if (batch.Count == 0) return false;

        using var gate = new SemaphoreSlim(_options.MaxParallelKeys);

        var results = await Task.WhenAll(batch.GroupBy(m => m.Key).Select(async group =>
        {
            await gate.WaitAsync(ct);
            try
            {
                return await DispatchKeyAsync(group.OrderBy(m => m.Id), ct);
            }
            finally
            {
                gate.Release();
            }
        }));

        await store.MarkCompletedAsync(results.SelectMany(r => r.Completed).ToList(),
            results.SelectMany(r => r.Dead).ToList(), ct);

        return batch.Count == _options.BatchSize;
    }

    private async Task<(List<long> Completed, List<long> Dead)> DispatchKeyAsync(IEnumerable<OutboxData> messages,
        CancellationToken ct)
    {
        var completed = new List<long>();
        var dead = new List<long>();

        foreach (var message in messages)
        {
            var outcome = await DispatchAsync(message, ct);
            if (outcome == DispatchOutcome.Failed && message.Attempts >= _options.MaxAttempts)
            {
                _logger.LogCritical(
                    "Outbox message {MessageId} for {Topic} exhausted {Attempts} attempts and is marked as dead",
                    message.MessageId, message.Topic, message.Attempts);
                outcome = DispatchOutcome.Exhausted;
            }

            if (outcome == DispatchOutcome.Failed) break;

            (outcome == DispatchOutcome.Published ? completed : dead).Add(message.Id);
        }

        return (completed, dead);
    }

    private async Task<DispatchOutcome> DispatchAsync(OutboxData data, CancellationToken ct)
    {
        MessagingCorrelation.CorrelationId = data.Headers.GetValueOrDefault(MessagingHeaders.CorrelationId);
        try
        {
            var message = KafkaMessageFactory.Create(data.Key, data.Payload, data.MessageType, data.MessageId,
                data.Headers);
            var result = await _producer.PublishAsync(data.Topic, message, ct);

            if (result.Success) return DispatchOutcome.Published;

            if (result.Error is { } error && PoisonCodes.Contains(error.Code))
            {
                _logger.LogCritical(
                    "Outbox message {MessageId} for {Topic} cannot be published and is marked as dead: {Error}",
                    data.MessageId, data.Topic, error.Reason);
                return DispatchOutcome.Poison;
            }

            _logger.LogWarning(
                "Failed to publish outbox message {MessageId} to {Topic}, attempt {Attempt}, retrying after the lease: {Error}",
                data.MessageId, data.Topic, data.Attempts, result.Error?.Reason);
            return DispatchOutcome.Failed;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex,
                "An unexpected error occurred while publishing outbox message {MessageId}, attempt {Attempt}",
                data.MessageId, data.Attempts);
            return DispatchOutcome.Failed;
        }
    }

    private enum DispatchOutcome
    {
        Published,
        Failed,
        Poison,
        Exhausted
    }
}
