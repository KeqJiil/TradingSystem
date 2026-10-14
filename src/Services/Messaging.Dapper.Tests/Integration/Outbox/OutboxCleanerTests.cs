using Messaging.Dapper.Outbox;
using Messaging.Dapper.Tests.Integration.Infrastructure;
using Messaging.Dapper.Transactions;
using Microsoft.Data.SqlClient;

namespace Messaging.Dapper.Tests.Integration.Outbox;

public class OutboxCleanerTests(MssqlFixture db) : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Cutoff = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly OutboxTable _table = new(db.ConnectionString);
    private UnitOfWork _unitOfWork = null!;
    private OutboxCleaner _sut = null!;

    public async Task InitializeAsync()
    {
        await _table.ClearAsync();
        _unitOfWork = new UnitOfWork(new SqlConnection(db.ConnectionString), []);
        _sut = new OutboxCleaner(_unitOfWork);
    }

    public async Task DisposeAsync()
    {
        await _unitOfWork.DisposeAsync();
    }

    [Fact]
    public async Task Cleanup_OldCompletedRows_DeletesThemInBatches()
    {
        await SeedAsync(5, "COMPLETED", Cutoff.AddDays(-1));

        await _sut.CleanupAsync(2, Cutoff, CancellationToken.None);

        Assert.Empty(await _table.GetAllAsync());
    }

    [Fact]
    public async Task Cleanup_CompletedRowsNotOlderThanCutoff_KeepsThem()
    {
        await SeedAsync(2, "COMPLETED", Cutoff.AddDays(1));
        await SeedAsync(1, "COMPLETED", Cutoff);

        await _sut.CleanupAsync(2, Cutoff, CancellationToken.None);

        Assert.Equal(3, (await _table.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Cleanup_MixedRows_ReturnsNumberOfDeletedRows()
    {
        await SeedAsync(5, "COMPLETED", Cutoff.AddMinutes(-1));
        await SeedAsync(3, "COMPLETED", Cutoff.AddMinutes(1));

        var deleted = await _sut.CleanupAsync(2, Cutoff, CancellationToken.None);

        Assert.Equal(5, deleted);
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("PROCESSING")]
    [InlineData("DEAD")]
    public async Task Cleanup_OldUnfinishedOrDeadRows_KeepsThem(string status)
    {
        await SeedAsync(3, status, Cutoff.AddDays(-30));

        var deleted = await _sut.CleanupAsync(2, Cutoff, CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.Equal(3, (await _table.GetAllAsync()).Count);
    }

    private async Task SeedAsync(int count, string status, DateTimeOffset createdAt)
    {
        for (var i = 0; i < count; i++)
            await _table.SeedAsync(new OutboxSeed { Status = status, CreatedAt = createdAt });
    }
}
