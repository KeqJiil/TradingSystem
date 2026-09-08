using Microsoft.Extensions.Internal;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Messaging.Workers;

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
        Func<Guid, long, T, CancellationToken, Task<ReadModelUpdateOutcome>> applyAsync, CancellationToken ct)
    {
        var outcome = await applyAsync(aggregateId, version, data, ct);

        switch (outcome)
        {
            case ReadModelUpdateOutcome.Applied:
                _firstGapSeenAt.Remove(aggregateId);
                await DrainBufferAsync(aggregateId, applyAsync, ct);
                break;

            case ReadModelUpdateOutcome.Stale:
                break;

            case ReadModelUpdateOutcome.Gap:
                BufferEvent(aggregateId, version, data);
                await CheckStuckGapAsync(aggregateId, ct);
                break;
        }
    }

    private void BufferEvent(Guid aggregateId, long version, T data)
    {
        if (!_pending.TryGetValue(aggregateId, out var buffer))
            _pending[aggregateId] = buffer = new SortedDictionary<long, T>();

        buffer[version] = data;
        _firstGapSeenAt.TryAdd(aggregateId, clock.UtcNow);
    }

    private async Task DrainBufferAsync(Guid aggregateId,
        Func<Guid, long, T, CancellationToken, Task<ReadModelUpdateOutcome>> applyAsync, CancellationToken ct)
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
    }

    private async Task CheckStuckGapAsync(Guid aggregateId, CancellationToken ct)
    {
        if (!_firstGapSeenAt.TryGetValue(aggregateId, out var since) || clock.UtcNow - since <= _gapTimeout)
            return;

        logger.LogWarning("{aggregateId} haven't been restored ordering for {gapTimeout} minutes", aggregateId,
            _gapTimeout);

        _firstGapSeenAt.Remove(aggregateId);

        if (_pending.Remove(aggregateId, out var buffer))
            await onExpire(aggregateId, buffer.Values.ToList(), ct);
    }
}