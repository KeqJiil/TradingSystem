using System.Text.Json;
using Dapper;
using Stock.Application.Events;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Infrastructure.Persistence;

public class OutboxWriterTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset CleanupCutoff = new(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly MssqlFixture _fixture;
    private TestDbContext _dbContext = null!;
    private OutboxWriter _sut = null!;

    public OutboxWriterTests(MssqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _dbContext = new TestDbContext(_fixture.ConnectionString);
        await _dbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(_dbContext);

        _sut = new OutboxWriter(_dbContext);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task WriteAsync_InsertsRow_WithPendingStatusAndSerializedPayload()
    {
        var aggregateId = Guid.NewGuid();
        var @event = new PriceChangedEvent(aggregateId, 12.5m, 1, DateTimeOffset.UtcNow);

        await _sut.WriteAsync(@event, aggregateId, CancellationToken.None);

        var row = await _dbContext.Connection.QuerySingleAsync<OutboxRow>(
            "SELECT aggregate_id AS AggregateId, event_type AS EventType, payload AS Payload, status AS Status, attempts AS Attempts " +
            "FROM outbox WHERE aggregate_id = @AggregateId", new { AggregateId = aggregateId });

        Assert.Equal(aggregateId, row.AggregateId);
        Assert.Equal(nameof(PriceChangedEvent), row.EventType);
        Assert.Equal("PENDING", row.Status);
        Assert.Equal(0, row.Attempts);
        Assert.Equal(@event, JsonSerializer.Deserialize<PriceChangedEvent>(row.Payload));
    }

    [Fact]
    public async Task WriteManyAsync_InsertsAllRows()
    {
        var events = Enumerable.Range(0, 3)
            .Select(i => (AggregateId: Guid.NewGuid(),
                Payload: new PriceChangedEvent(Guid.NewGuid(), i, i, DateTimeOffset.UtcNow)))
            .ToList();

        await _sut.WriteManyAsync(events, CancellationToken.None);

        var count = await _dbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM outbox WHERE aggregate_id IN @Ids",
            new { Ids = events.Select(e => e.AggregateId) });

        Assert.Equal(events.Count, count);
    }

    [Fact]
    public async Task WriteManyAsync_WithEmptyCollection_InsertsNothing()
    {
        await _sut.WriteManyAsync(Array.Empty<(Guid, PriceChangedEvent)>(), CancellationToken.None);

        var count = await _dbContext.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM outbox");

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task MarkCompletedAsync_UpdatesStatus_OnlyForGivenIds()
    {
        var toComplete = await InsertOutboxRowAsync();
        var untouched = await InsertOutboxRowAsync();

        await _sut.MarkCompletedAsync([toComplete], CancellationToken.None);

        var statuses = await _dbContext.Connection.QueryAsync<(Guid Id, string Status)>(
            "SELECT id AS Id, status AS Status FROM outbox WHERE id IN @Ids",
            new { Ids = new[] { toComplete, untouched } });

        var statusById = statuses.ToDictionary(s => s.Id, s => s.Status);
        Assert.Equal("COMPLETED", statusById[toComplete]);
        Assert.Equal("PENDING", statusById[untouched]);
    }

    [Theory]
    [InlineData(5, 2)]
    [InlineData(4, 2)]
    [InlineData(3, 5)]
    public async Task CleanupAsync_DeletesAllOldCompletedRows_AcrossBatches(int rows, int batchSize)
    {
        for (var i = 0; i < rows; i++)
            await InsertOutboxRowAsync("COMPLETED", CleanupCutoff.AddDays(-1));

        var deleted = await _sut.CleanupAsync(CleanupCutoff, batchSize, CancellationToken.None);

        Assert.Equal(rows, deleted);
        Assert.Equal(0, await _dbContext.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM outbox"));
    }

    [Fact]
    public async Task CleanupAsync_KeepsNotCompletedRows_AndRowsNotOlderThanCutoff()
    {
        var oldCompleted = await InsertOutboxRowAsync("COMPLETED", CleanupCutoff.AddDays(-1));
        var kept = new[]
        {
            await InsertOutboxRowAsync("PENDING", CleanupCutoff.AddDays(-1)),
            await InsertOutboxRowAsync("PROCESSING", CleanupCutoff.AddDays(-1)),
            await InsertOutboxRowAsync("COMPLETED", CleanupCutoff),
            await InsertOutboxRowAsync("COMPLETED", CleanupCutoff.AddMinutes(1))
        };

        var deleted = await _sut.CleanupAsync(CleanupCutoff, 100, CancellationToken.None);

        var remaining = await _dbContext.Connection.QueryAsync<Guid>("SELECT id FROM outbox");
        Assert.Equal(1, deleted);
        Assert.DoesNotContain(oldCompleted, remaining);
        Assert.Equal(kept.Order(), remaining.Order());
    }

    private async Task<Guid> InsertOutboxRowAsync()
    {
        var id = Guid.NewGuid();

        await _dbContext.Connection.ExecuteAsync("""
                                                 INSERT INTO outbox (id, aggregate_id, payload, event_type, status)
                                                 VALUES (@Id, @AggregateId, @Payload, @EventType, 'PENDING')
                                                 """,
            new { Id = id, AggregateId = Guid.NewGuid(), Payload = "{}", EventType = "TestEvent" });

        return id;
    }

    private async Task<Guid> InsertOutboxRowAsync(string status, DateTimeOffset createdAt)
    {
        var id = Guid.NewGuid();

        await _dbContext.Connection.ExecuteAsync("""
                                                 INSERT INTO outbox (id, aggregate_id, payload, event_type, status, created_at)
                                                 VALUES (@Id, @AggregateId, '{}', 'TestEvent', @Status, @CreatedAt)
                                                 """,
            new { Id = id, AggregateId = Guid.NewGuid(), Status = status, CreatedAt = createdAt });

        return id;
    }

    private record OutboxRow(Guid AggregateId, string EventType, string Payload, string Status, int Attempts);
}