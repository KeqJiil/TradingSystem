using Dapper;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Infrastructure.Persistence;

public class OutboxReaderTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly MssqlFixture _fixture;
    private TestDbContext _dbContext = null!;
    private OutboxReader _sut = null!;

    public OutboxReaderTests(MssqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _dbContext = new TestDbContext(_fixture.ConnectionString);
        await _dbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await OutboxTestSchema.EnsureCreatedAsync(_dbContext);
        await _dbContext.Connection.ExecuteAsync("DELETE FROM outbox");

        _sut = new OutboxReader(_dbContext);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetPendingAsync_ReturnsRecordsWithoutProcessedAt()
    {
        var id = await InsertOutboxRowAsync("PENDING", null);

        var result = await _sut.GetPendingAsync(10, 5, CancellationToken.None);

        Assert.Contains(result, r => r.Id == id);
    }

    [Fact]
    public async Task GetPendingAsync_ReturnsRecordsProcessedLongerAgoThanMaxWait()
    {
        var id = await InsertOutboxRowAsync("PROCESSING", DateTimeOffset.UtcNow.AddMinutes(-10));

        var result = await _sut.GetPendingAsync(10, 5, CancellationToken.None);

        Assert.Contains(result, r => r.Id == id);
    }

    [Fact]
    public async Task GetPendingAsync_ExcludesRecentlyProcessedRecords()
    {
        var id = await InsertOutboxRowAsync("PROCESSING", DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = await _sut.GetPendingAsync(10, 5, CancellationToken.None);

        Assert.DoesNotContain(result, r => r.Id == id);
    }

    [Fact]
    public async Task GetPendingAsync_ExcludesCompletedRecords()
    {
        var id = await InsertOutboxRowAsync("COMPLETED", null);

        var result = await _sut.GetPendingAsync(10, 5, CancellationToken.None);

        Assert.DoesNotContain(result, r => r.Id == id);
    }

    [Fact]
    public async Task GetPendingAsync_ExcludesRecordsThatReachedAttemptLimit()
    {
        var id = await InsertOutboxRowAsync("PENDING", null, 3);

        var result = await _sut.GetPendingAsync(10, 5, CancellationToken.None);

        Assert.DoesNotContain(result, r => r.Id == id);
    }

    [Fact]
    public async Task GetPendingAsync_RespectsAmountLimit()
    {
        for (var i = 0; i < 5; i++)
            await InsertOutboxRowAsync("PENDING", null);

        var result = await _sut.GetPendingAsync(2, 5, CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetPendingAsync_MarksReturnedRowsAsProcessingAndIncrementsAttempts()
    {
        var id = await InsertOutboxRowAsync("PENDING", null, 1);

        await _sut.GetPendingAsync(10, 5, CancellationToken.None);

        var row = await _dbContext.Connection.QuerySingleAsync<(string Status, int Attempts)>(
            "SELECT status AS Status, attempts AS Attempts FROM outbox WHERE id = @Id", new { Id = id });

        Assert.Equal("PROCESSING", row.Status);
        Assert.Equal(2, row.Attempts);
    }

    private async Task<Guid> InsertOutboxRowAsync(string status, DateTimeOffset? processedAt, int attempts = 0)
    {
        var id = Guid.NewGuid();

        await _dbContext.Connection.ExecuteAsync("""
                                                 INSERT INTO outbox (id, aggregate_id, payload, event_type, status, processed_at, attempts)
                                                 VALUES (@Id, @AggregateId, @Payload, @EventType, @Status, @ProcessedAt, @Attempts)
                                                 """, new
        {
            Id = id,
            AggregateId = Guid.NewGuid(),
            Payload = "{}",
            EventType = "TestEvent",
            Status = status,
            ProcessedAt = processedAt,
            Attempts = attempts
        });

        return id;
    }
}