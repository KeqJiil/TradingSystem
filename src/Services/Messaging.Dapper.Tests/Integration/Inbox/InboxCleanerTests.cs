using Dapper;
using Messaging.Dapper.Inbox;
using Messaging.Dapper.Tests.Integration.Infrastructure;
using Messaging.Dapper.Transactions;
using Microsoft.Data.SqlClient;

namespace Messaging.Dapper.Tests.Integration.Inbox;

public class InboxCleanerTests(MssqlFixture db) : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Cutoff = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private UnitOfWork _unitOfWork = null!;
    private InboxCleaner _sut = null!;


    [Fact]
    public async Task Cleanup_MoreOldRowsThanBatchSize_DeletesAllOldRowsInBatches()
    {
        await SeedAsync(5, Cutoff.AddDays(-1));

        var deleted = await _sut.CleanupAsync(2, Cutoff, CancellationToken.None);

        Assert.Equal(5, deleted);
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task Cleanup_RowsNewerThanCutoff_KeepsThem()
    {
        await SeedAsync(3, Cutoff.AddDays(-1));
        await SeedAsync(2, Cutoff.AddDays(1));
        await SeedAsync(1, Cutoff);

        await _sut.CleanupAsync(2, Cutoff, CancellationToken.None);

        Assert.Equal(3, await CountAsync());
        Assert.Equal(0, await CountAsync(Cutoff));
    }

    [Fact]
    public async Task Cleanup_MixedRows_ReturnsNumberOfDeletedOldRows()
    {
        await SeedAsync(4, Cutoff.AddMinutes(-1));
        await SeedAsync(3, Cutoff.AddMinutes(1));

        var deleted = await _sut.CleanupAsync(3, Cutoff, CancellationToken.None);

        Assert.Equal(4, deleted);
    }

    [Fact]
    public async Task Cleanup_NoOldRows_ReturnsZero()
    {
        await SeedAsync(2, Cutoff.AddDays(1));

        var deleted = await _sut.CleanupAsync(2, Cutoff, CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.Equal(2, await CountAsync());
    }

    public async Task InitializeAsync()
    {
        await using var connection = await db.OpenConnectionAsync();
        await connection.ExecuteAsync("DELETE FROM messaging.inbox");

        _unitOfWork = new UnitOfWork(new SqlConnection(db.ConnectionString), []);
        _sut = new InboxCleaner(_unitOfWork);
    }

    public async Task DisposeAsync()
    {
        await _unitOfWork.DisposeAsync();
    }
    
    private async Task SeedAsync(int count, DateTimeOffset createdAt)
    {
        await using var connection = await db.OpenConnectionAsync();
        for (var i = 0; i < count; i++)
            await connection.ExecuteAsync(
                "INSERT INTO messaging.inbox (consumer_group, message_id, created_at) VALUES (@Group, @Id, @CreatedAt)",
                new { Group = "orders", Id = Guid.NewGuid().ToString(), CreatedAt = createdAt });
    }

    private async Task<int> CountAsync(DateTimeOffset? olderThan = null)
    {
        await using var connection = await db.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM messaging.inbox WHERE @OlderThan IS NULL OR created_at < @OlderThan",
            new { OlderThan = olderThan });
    }
    
}
