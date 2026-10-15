using System.Text.Json;
using Messaging.EFCore.Outbox;
using Messaging.EFCore.Tests.Infrastructure;

namespace Messaging.EFCore.Tests.Outbox;

public class OutboxWriterTests(MssqlFixture db) : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly OutboxTable _table = new(db.ConnectionString);
    private TestDbContext _context = null!;
    private OutboxWriter<TestDbContext> _sut = null!;

    public async Task InitializeAsync()
    {
        await _table.ClearAsync();
        _context = db.CreateContext();
        _sut = new OutboxWriter<TestDbContext>(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Write_Entry_InsertsPendingRowWithAllFields()
    {
        var entry = Entry("order-1");

        await _sut.WriteAsync(entry, CancellationToken.None);
        await _context.SaveChangesAsync();

        var row = Assert.Single(await _table.GetAllAsync());
        AssertPending(entry, row);
    }

    [Fact]
    public async Task WriteMany_Entries_InsertsAllAsPending()
    {
        var entries = new[] { Entry("a"), Entry("b"), Entry("c") };

        await _sut.WriteManyAsync(entries, CancellationToken.None);
        await _context.SaveChangesAsync();

        var rows = await _table.GetAllAsync();
        Assert.Equal(3, rows.Count);
        foreach (var entry in entries)
            AssertPending(entry, rows.Single(r => r.MessageId == entry.MessageId));
    }

    [Fact]
    public async Task WriteMany_Entries_IdsAscendInInputOrder()
    {
        var entries = Enumerable.Range(0, 10).Select(i => Entry($"key-{i}")).ToList();

        await _sut.WriteManyAsync(entries, CancellationToken.None);
        await _context.SaveChangesAsync();

        var rows = await _table.GetAllAsync();
        Assert.Equal(entries.Select(e => e.MessageId), rows.Select(r => r.MessageId));
    }

    [Fact]
    public async Task Write_SeveralCalls_IdsAscendInCallOrder()
    {
        var entries = Enumerable.Range(0, 5).Select(i => Entry($"key-{i}")).ToList();

        foreach (var entry in entries)
            await _sut.WriteAsync(entry, CancellationToken.None);
        await _context.SaveChangesAsync();

        var rows = await _table.GetAllAsync();
        Assert.Equal(entries.Select(e => e.MessageId), rows.Select(r => r.MessageId));
    }

    [Fact]
    public async Task MarkCompleted_CompletedAndDeadIds_SetsStatusesAndLeavesOtherRowsUntouched()
    {
        var completed = await _table.SeedAsync(new OutboxSeed { Status = "PROCESSING", Attempts = 1 });
        var dead = await _table.SeedAsync(new OutboxSeed { Status = "PROCESSING", Attempts = 1 });
        var other = await _table.SeedAsync(new OutboxSeed { Status = "PROCESSING", Attempts = 1 });

        await _sut.MarkCompletedAsync([completed], [dead], CancellationToken.None);

        Assert.Equal("COMPLETED", (await _table.GetAsync(completed))!.Status);
        Assert.Equal("DEAD", (await _table.GetAsync(dead))!.Status);
        Assert.Equal("PROCESSING", (await _table.GetAsync(other))!.Status);
    }

    [Fact]
    public async Task MarkCompleted_OnlyCompletedIds_MarksThemCompleted()
    {
        var completed = await _table.SeedAsync(new OutboxSeed { Status = "PROCESSING" });

        await _sut.MarkCompletedAsync([completed], [], CancellationToken.None);

        Assert.Equal("COMPLETED", (await _table.GetAsync(completed))!.Status);
    }

    [Fact]
    public async Task MarkCompleted_OnlyDeadIds_MarksThemDead()
    {
        var dead = await _table.SeedAsync(new OutboxSeed { Status = "PROCESSING" });

        await _sut.MarkCompletedAsync([], [dead], CancellationToken.None);

        Assert.Equal("DEAD", (await _table.GetAsync(dead))!.Status);
    }

    [Fact]
    public async Task MarkCompleted_EmptyLists_ChangesNothing()
    {
        var id = await _table.SeedAsync(new OutboxSeed { Status = "PROCESSING" });

        await _sut.MarkCompletedAsync([], [], CancellationToken.None);

        Assert.Equal("PROCESSING", (await _table.GetAsync(id))!.Status);
    }

    private static OutboxEntity Entry(string key)
    {
        return new OutboxEntity
        {
            MessageId = Guid.NewGuid().ToString(),
            MessageType = "OrderPlaced",
            Topic = "orders",
            MessageKey = key,
            Payload = [1, 2, 3],
            Headers = JsonSerializer.Serialize(new Dictionary<string, string> { ["correlation-id"] = $"c-{key}" })
        };
    }

    private static void AssertPending(OutboxEntity entry, OutboxStoredRow row)
    {
        Assert.Equal(entry.MessageId, row.MessageId);
        Assert.Equal(entry.MessageType, row.MessageType);
        Assert.Equal(entry.Topic, row.Topic);
        Assert.Equal(entry.MessageKey, row.MessageKey);
        Assert.Equal(entry.Payload, row.Payload);
        Assert.Equal(entry.Headers, row.Headers);
        Assert.Equal("PENDING", row.Status);
        Assert.Equal(0, row.Attempts);
        Assert.Null(row.ProcessedAt);
    }
}
