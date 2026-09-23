using Microsoft.Extensions.Internal;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Messaging.Consumers;

public class VersionsBuffer<T>(
    ILogger<VersionsBuffer<T>> logger,
    ISystemClock clock,
    Func<Guid, IReadOnlyCollection<T>, CancellationToken, Task> onExpire)
{
    private readonly Dictionary<Guid, SortedDictionary<long, T>> _pending = new();
    private readonly Dictionary<Guid, DateTimeOffset> _firstGapSeenAt = new();
    private readonly TimeSpan _gapTimeout = TimeSpan.FromMinutes(2);

    public bool HasPendingGaps => _pending.Count > 0;

    public async Task TryApplyAsync(Guid aggregateId, long version, T data,
        Func<Guid, long, T, CancellationToken, Task<ReadModelUpdateOutcome>> applyAsync,
        CancellationToken ct)
    {
        var outcome = await applyAsync(aggregateId, version, data, ct);

        switch (outcome)
        {
            case ReadModelUpdateOutcome.Applied:
                await DrainBufferAsync(aggregateId, applyAsync, ct);
                break;

            case ReadModelUpdateOutcome.Stale:
                break;

            case ReadModelUpdateOutcome.Gap:
                BufferEvent(aggregateId, version, data);
                break;
        }

        await ExpireStuckGapsAsync(ct);
    }

    private void BufferEvent(Guid aggregateId, long version, T data)
    {
        if (!_pending.TryGetValue(aggregateId, out var buffer))
            _pending[aggregateId] = buffer = new SortedDictionary<long, T>();

        buffer[version] = data;
        _firstGapSeenAt.TryAdd(aggregateId, clock.UtcNow);
    }

    private async Task DrainBufferAsync(Guid aggregateId,
        Func<Guid, long, T, CancellationToken, Task<ReadModelUpdateOutcome>> applyAsync,
        CancellationToken ct)
    {
        if (!_pending.TryGetValue(aggregateId, out var buffer)) return;

        while (buffer.Count > 0)
        {
            var (version, data) = buffer.First();
            var outcome = await applyAsync(aggregateId, version, data, ct);
            if (outcome != ReadModelUpdateOutcome.Applied) break;

            buffer.Remove(version);
        }

        if (buffer.Count == 0)
        {
            _pending.Remove(aggregateId);
            _firstGapSeenAt.Remove(aggregateId);
        }
        else
        {
            _firstGapSeenAt[aggregateId] = clock.UtcNow;
        }
    }

    private async Task ExpireStuckGapsAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var stuck = _firstGapSeenAt
            .Where(gap => now - gap.Value > _gapTimeout)
            .Select(gap => gap.Key)
            .ToList();

        foreach (var aggregateId in stuck)
        {
            if (!_pending.TryGetValue(aggregateId, out var buffer)) continue;

            logger.LogWarning(
                "Ordering for {AggregateId} hasn't been restored for {GapTimeoutMinutes} minutes, handing over {BufferedCount} buffered events",
                aggregateId, _gapTimeout.TotalMinutes, buffer.Count);

            try
            {
                await onExpire(aggregateId, buffer.Values.ToList(), ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Failed to hand over expired events of {AggregateId}, keeping them buffered",
                    aggregateId);
                continue;
            }

            _pending.Remove(aggregateId);
            _firstGapSeenAt.Remove(aggregateId);
        }
    }
}