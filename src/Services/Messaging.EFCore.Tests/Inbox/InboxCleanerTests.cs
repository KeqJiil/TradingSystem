using Dapper;
using Messaging.EFCore.Inbox;
using Messaging.EFCore.Tests.Infrastructure;

namespace Messaging.EFCore.Tests.Inbox;

public class InboxCleanerTests(MssqlFixture db) : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Cutoff = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private TestDbContext _context = null!;
    private InboxCleanser<TestDbContext> _sut = null!;

    [Fact]
    public async Task Cleanup_RowsOlderThanCutoff_DeletesThem()
    {
        await SeedAsync(3, Cutoff.AddDays(-1));

        await _sut.CleanupAsync(100, Cutoff, CancellationToken.None);

        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task Cleanup_RowsNewerThanCutoff_KeepsThem()
    {
        await SeedAsync(3, Cutoff.AddDays(1));

        await _sut.CleanupAsync(100, Cutoff, CancellationToken.None);

        Assert.Equal(3, await CountAsync());
    }

    [Fact]
    public async Task Cleanup_RowExactlyAtCutoff_KeepsIt()
    {
        await SeedAsync(1, Cutoff);

        await _sut.CleanupAsync(100, Cutoff, CancellationToken.None);

        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task Cleanup_MixedRows_ReturnsNumberOfDeletedRows()
    {
        await SeedAsync(3, Cutoff.AddDays(-1));
        await SeedAsync(2, Cutoff.AddDays(1));

        var deleted = await _sut.CleanupAsync(100, Cutoff, CancellationToken.None);

        Assert.Equal(3, deleted);
        Assert.Equal(2, await CountAsync());
    }

    [Fact]
    public async Task Cleanup_MoreOldRowsThanBatchSize_DeletesAllOldRowsInBatches()
    {
        await SeedAsync(5, Cutoff.AddDays(-1));

        var deleted = await _sut.CleanupAsync(2, Cutoff, CancellationToken.None);

        Assert.Equal(5, deleted);
        Assert.Equal(0, await CountAsync());
    }

    public async Task InitializeAsync()
    {
        await using var connection = await db.OpenConnectionAsync();
        await connection.ExecuteAsync("DELETE FROM inbox");

        _context = db.CreateContext();
        _sut = new InboxCleanser<TestDbContext>(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private async Task SeedAsync(int count, DateTimeOffset createdAt)
    {
        await using var connection = await db.OpenConnectionAsync();
        for (var i = 0; i < count; i++)
            await connection.ExecuteAsync(
                "INSERT INTO inbox (consumer_group, message_id, created_at) VALUES (@Group, @Id, @CreatedAt)",
                new { Group = "orders", Id = Guid.NewGuid().ToString(), CreatedAt = createdAt });
    }

    private async Task<int> CountAsync()
    {
        await using var connection = await db.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inbox");
    }
}
