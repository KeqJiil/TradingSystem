using Messaging.EFCore.Outbox;
using Messaging.EFCore.Tests.Infrastructure;

namespace Messaging.EFCore.Tests.Outbox;

public class OutboxReaderTests(MssqlFixture db) : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private const int LeaseMinutes = 2;

    private readonly OutboxTable _table = new(db.ConnectionString);
    private TestDbContext _context = null!;
    private OutboxReader<TestDbContext> _sut = null!;

    public async Task InitializeAsync()
    {
        await _table.ClearAsync();
        _context = db.CreateContext();
        _sut = new OutboxReader<TestDbContext>(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetPending_PendingRow_IsReturned()
    {
        var id = await _table.SeedAsync(new OutboxSeed());

        var result = await _sut.GetPendingAsync(10, LeaseMinutes, CancellationToken.None);

        Assert.Equal(id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task GetPending_ProcessingRowWithExpiredLease_IsReturned()
    {
        var id = await _table.SeedAsync(new OutboxSeed
        {
            Status = "PROCESSING", Attempts = 1, ProcessedAt = DateTimeOffset.UtcNow.AddMinutes(-LeaseMinutes - 1)
        });

        var result = await _sut.GetPendingAsync(10, LeaseMinutes, CancellationToken.None);

        Assert.Equal(id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task GetPending_ProcessingRowWithActiveLease_IsNotReturned()
    {
        await _table.SeedAsync(new OutboxSeed
        {
            Status = "PROCESSING", Attempts = 1, ProcessedAt = DateTimeOffset.UtcNow.AddSeconds(-10)
        });

        var result = await _sut.GetPendingAsync(10, LeaseMinutes, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPending_ClaimedRow_IsMarkedProcessingWithIncrementedAttemptsAndFreshLease()
    {
        var id = await _table.SeedAsync(new OutboxSeed
        {
            Status = "PROCESSING", Attempts = 2, ProcessedAt = DateTimeOffset.UtcNow.AddMinutes(-LeaseMinutes - 1)
        });
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);

        var returned = Assert.Single(await _sut.GetPendingAsync(10, LeaseMinutes, CancellationToken.None));

        var row = await _table.GetAsync(id);
        Assert.Equal(3, returned.Attempts);
        Assert.Equal("PROCESSING", row!.Status);
        Assert.Equal(3, row.Attempts);
        Assert.True(row.ProcessedAt > before);
    }

    [Fact]
    public async Task GetPending_SeveralRows_ReturnedInIdOrder()
    {
        var ids = new List<long>();
        for (var i = 0; i < 5; i++)
            ids.Add(await _table.SeedAsync(new OutboxSeed { Key = $"key-{i % 2}" }));

        var result = await _sut.GetPendingAsync(10, LeaseMinutes, CancellationToken.None);

        Assert.Equal(ids, result.Select(r => r.Id));
    }

    [Fact]
    public async Task GetPending_MoreRowsThanAmount_ReturnsFirstRowsAndLeavesRestUntouched()
    {
        var ids = new List<long>();
        for (var i = 0; i < 3; i++)
            ids.Add(await _table.SeedAsync(new OutboxSeed()));

        var result = await _sut.GetPendingAsync(2, LeaseMinutes, CancellationToken.None);

        Assert.Equal(ids.Take(2), result.Select(r => r.Id));
        var rest = await _table.GetAsync(ids[2]);
        Assert.Equal("PENDING", rest!.Status);
        Assert.Equal(0, rest.Attempts);
        Assert.Null(rest.ProcessedAt);
    }

    [Fact]
    public async Task GetPending_RowsNotEligible_AreLeftUntouched()
    {
        var leasedAt = DateTimeOffset.UtcNow.AddSeconds(-10);
        var leased = await _table.SeedAsync(new OutboxSeed
            { Status = "PROCESSING", Attempts = 1, ProcessedAt = leasedAt });
        var completed = await _table.SeedAsync(new OutboxSeed { Status = "COMPLETED", Attempts = 1 });
        var dead = await _table.SeedAsync(new OutboxSeed { Status = "DEAD", Attempts = 1 });

        await _sut.GetPendingAsync(10, LeaseMinutes, CancellationToken.None);

        var leasedRow = await _table.GetAsync(leased);
        Assert.Equal(1, leasedRow!.Attempts);
        Assert.Equal(leasedAt, leasedRow.ProcessedAt!.Value, TimeSpan.FromMilliseconds(10));
        Assert.Equal(1, (await _table.GetAsync(completed))!.Attempts);
        Assert.Equal(1, (await _table.GetAsync(dead))!.Attempts);
    }

    [Theory]
    [InlineData("COMPLETED")]
    [InlineData("DEAD")]
    public async Task GetPending_FinishedRow_IsNotReturned(string status)
    {
        await _table.SeedAsync(new OutboxSeed { Status = status });

        var result = await _sut.GetPendingAsync(10, LeaseMinutes, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPending_ReturnedRow_CarriesStoredData()
    {
        var seed = new OutboxSeed
        {
            MessageType = "OrderPlaced", Topic = "orders", Key = "order-1", Payload = [9, 8, 7],
            Headers = new Dictionary<string, string> { ["correlation-id"] = "c-1" }
        };
        await _table.SeedAsync(seed);

        var row = Assert.Single(await _sut.GetPendingAsync(10, LeaseMinutes, CancellationToken.None));

        Assert.Equal(seed.MessageId, row.MessageId);
        Assert.Equal(seed.MessageType, row.MessageType);
        Assert.Equal(seed.Topic, row.Topic);
        Assert.Equal(seed.Key, row.Key);
        Assert.Equal(seed.Payload, row.Payload);
        Assert.Equal(seed.Headers, row.Headers);
    }
}
