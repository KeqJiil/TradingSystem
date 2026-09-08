using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Stock.Application.Abstractions;
using Stock.Application.Events;
using Stock.Infrastructure.Messaging.Consumers;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Consumers;

public class VersionsBufferTests
{
    private static readonly ILogger<VersionsBuffer<PriceChangedEvent>> Logger =
        NullLogger<VersionsBuffer<PriceChangedEvent>>.Instance;

    private readonly FakeSystemClock _clock = new(DateTimeOffset.UtcNow);
    private readonly VersionsBuffer<PriceChangedEvent> _versionsBuffer;
    private readonly FakeApplier _fakeApplier = new();
    private readonly List<(Guid AggregateId, IReadOnlyCollection<PriceChangedEvent> Expired)> _expired = new();

    public VersionsBufferTests()
    {
        _versionsBuffer = new VersionsBuffer<PriceChangedEvent>(Logger, _clock, OnExpire);
    }

    private Task OnExpire(Guid aggregateId, IReadOnlyCollection<PriceChangedEvent> expired, CancellationToken ct)
    {
        _expired.Add((aggregateId, expired));
        return Task.CompletedTask;
    }

    [Fact]
    public async Task HappyPath_Applied()
    {
        var aggregateId = Guid.NewGuid();
        var version = 1L;

        await _versionsBuffer.TryApplyAsync(aggregateId, version,
            new PriceChangedEvent(aggregateId, -10, 1, DateTimeOffset.UtcNow),
            _fakeApplier.ApplyAsync, CancellationToken.None);

        Assert.False(_versionsBuffer.HasPendingGaps);
    }

    [Fact]
    public async Task Gap_Scenario()
    {
        var aggregateId = Guid.NewGuid();
        var version1 = 1L;
        var version2 = 3L;

        await _versionsBuffer.TryApplyAsync(aggregateId, version1,
            new PriceChangedEvent(aggregateId, -10, 1, DateTimeOffset.UtcNow),
            _fakeApplier.ApplyAsync, CancellationToken.None);

        await _versionsBuffer.TryApplyAsync(aggregateId, version2,
            new PriceChangedEvent(aggregateId, -10, 3, DateTimeOffset.UtcNow),
            _fakeApplier.ApplyAsync, CancellationToken.None);

        Assert.True(_versionsBuffer.HasPendingGaps);
    }

    [Fact]
    public async Task GapThenApplied_Scenario()
    {
        var aggregateId = Guid.NewGuid();
        var version1 = 1L;
        var version2 = 3L;
        var version3 = 2L;

        await _versionsBuffer.TryApplyAsync(aggregateId, version1,
            new PriceChangedEvent(aggregateId, -10, 1, DateTimeOffset.UtcNow),
            _fakeApplier.ApplyAsync, CancellationToken.None);

        await _versionsBuffer.TryApplyAsync(aggregateId, version2,
            new PriceChangedEvent(aggregateId, -10, 3, DateTimeOffset.UtcNow),
            _fakeApplier.ApplyAsync, CancellationToken.None);

        Assert.True(_versionsBuffer.HasPendingGaps);

        await _versionsBuffer.TryApplyAsync(aggregateId, version3,
            new PriceChangedEvent(aggregateId, -10, 2, DateTimeOffset.UtcNow),
            _fakeApplier.ApplyAsync, CancellationToken.None);

        Assert.False(_versionsBuffer.HasPendingGaps);
        Assert.Equal(
            new[] { (aggregateId, 1L), (aggregateId, 3L), (aggregateId, 2L), (aggregateId, 3L) },
            _fakeApplier.Calls);
    }

    [Fact]
    public async Task Stale_Scenario()
    {
        var aggregateId = Guid.NewGuid();
        var version1 = 1L;
        var version2 = 1L;

        await _versionsBuffer.TryApplyAsync(aggregateId, version1,
            new PriceChangedEvent(aggregateId, -10, 1, DateTimeOffset.UtcNow),
            _fakeApplier.ApplyAsync, CancellationToken.None);

        Assert.False(_versionsBuffer.HasPendingGaps);

        await _versionsBuffer.TryApplyAsync(aggregateId, version2,
            new PriceChangedEvent(aggregateId, -10, 1, DateTimeOffset.UtcNow),
            _fakeApplier.ApplyAsync, CancellationToken.None);

        Assert.False(_versionsBuffer.HasPendingGaps);
    }

    [Fact]
    public async Task MultipleAggregateIds_Scenario()
    {
        var aggregateId1 = Guid.NewGuid();
        var aggregateId2 = Guid.NewGuid();

        await _versionsBuffer.TryApplyAsync(aggregateId1, 1,
            new PriceChangedEvent(aggregateId1, -10, 1, DateTimeOffset.UtcNow),
            _fakeApplier.ApplyAsync, CancellationToken.None);

        await _versionsBuffer.TryApplyAsync(aggregateId2, 1,
            new PriceChangedEvent(aggregateId2, -10, 1, DateTimeOffset.UtcNow),
            _fakeApplier.ApplyAsync, CancellationToken.None);

        Assert.False(_versionsBuffer.HasPendingGaps);
    }

    [Fact]
    public async Task StuckGap_ExpiresAfterTimeout()
    {
        var aggregateId = Guid.NewGuid();
        var firstGapEvent = new PriceChangedEvent(aggregateId, -10, 3, DateTimeOffset.UtcNow);

        await _versionsBuffer.TryApplyAsync(aggregateId, 3, firstGapEvent,
            _fakeApplier.ApplyAsync, CancellationToken.None);

        Assert.True(_versionsBuffer.HasPendingGaps);
        Assert.Empty(_expired);

        _clock.UtcNow += TimeSpan.FromMinutes(3);

        var secondGapEvent = new PriceChangedEvent(aggregateId, -10, 4, DateTimeOffset.UtcNow);

        await _versionsBuffer.TryApplyAsync(aggregateId, 4, secondGapEvent,
            _fakeApplier.ApplyAsync, CancellationToken.None);

        Assert.False(_versionsBuffer.HasPendingGaps);

        var (expiredAggregateId, expired) = Assert.Single(_expired);
        Assert.Equal(aggregateId, expiredAggregateId);
        Assert.Equal([firstGapEvent, secondGapEvent], expired);
    }

    [Fact]
    public async Task Gap_DoesNotExpireBeforeTimeout()
    {
        var aggregateId = Guid.NewGuid();

        await _versionsBuffer.TryApplyAsync(aggregateId, 3,
            new PriceChangedEvent(aggregateId, -10, 3, DateTimeOffset.UtcNow),
            _fakeApplier.ApplyAsync, CancellationToken.None);

        _clock.UtcNow += TimeSpan.FromMinutes(1);

        await _versionsBuffer.TryApplyAsync(aggregateId, 3,
            new PriceChangedEvent(aggregateId, -10, 3, DateTimeOffset.UtcNow),
            _fakeApplier.ApplyAsync, CancellationToken.None);

        Assert.True(_versionsBuffer.HasPendingGaps);
    }
}

internal class FakeSystemClock(DateTimeOffset utcNow) : ISystemClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}

internal class FakeApplier
{
    private readonly Dictionary<Guid, long> _currentVersion = new();
    public readonly List<(Guid AggregateId, long Version)> Calls = new();

    public Task<ReadModelUpdateOutcome> ApplyAsync(Guid aggregateId, long version, PriceChangedEvent data,
        CancellationToken ct)
    {
        Calls.Add((aggregateId, version));
        var current = _currentVersion.GetValueOrDefault(aggregateId, 0);

        if (version <= current) return Task.FromResult(ReadModelUpdateOutcome.Stale);
        if (version > current + 1) return Task.FromResult(ReadModelUpdateOutcome.Gap);

        _currentVersion[aggregateId] = version;
        return Task.FromResult(ReadModelUpdateOutcome.Applied);
    }
}