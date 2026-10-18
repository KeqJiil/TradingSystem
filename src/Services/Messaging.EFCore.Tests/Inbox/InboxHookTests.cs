using Dapper;
using Messaging.EFCore.Inbox;
using Messaging.EFCore.Outbox;
using Messaging.EFCore.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Messaging.EFCore.Tests.Inbox;

public class InboxHookTests(MssqlFixture db) : IClassFixture<MssqlFixture>
{
    private const string Group = "orders";

    private readonly InboxState _state = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveChanges_Succeeded_MarksStateCommitted(bool async)
    {
        _state.Set(NewId(), Group);
        await using var context = CreateContext();

        await SaveAsync(context, async);

        Assert.True(_state.Committed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveChanges_Failed_LeavesStateUncommitted(bool async)
    {
        var id = NewId();
        await SeedInboxAsync(id);
        _state.Set(id, Group);
        await using var context = CreateContext();

        await Assert.ThrowsAsync<DbUpdateException>(() => SaveAsync(context, async));

        Assert.False(_state.Committed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveChanges_StateSet_AddsInboxEntity(bool async)
    {
        var id = NewId();
        _state.Set(id, Group);
        await using var context = CreateContext();

        await SaveAsync(context, async);

        Assert.Equal(1, await CountInboxAsync(id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveChanges_StateNotSet_DoesNotAddInboxEntity(bool async)
    {
        await using var context = CreateContext();
        context.Add(NewOutbox());

        await SaveAsync(context, async);

        Assert.Empty(context.ChangeTracker.Entries<InboxEntity>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveChanges_CalledTwice_AddsSingleInboxEntity(bool async)
    {
        var id = NewId();
        _state.Set(id, Group);
        await using var context = CreateContext();

        await SaveAsync(context, async);
        context.Add(NewOutbox());
        await SaveAsync(context, async);

        Assert.Equal(1, await CountInboxAsync(id));
    }

    private TestDbContext CreateContext()
    {
        return new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(db.ConnectionString)
            .AddInterceptors(new InboxHook(_state))
            .Options);
    }

    private static async Task SaveAsync(DbContext context, bool async)
    {
        if (async) await context.SaveChangesAsync();
        else context.SaveChanges();
    }

    private static string NewId()
    {
        return Guid.NewGuid().ToString();
    }

    private static OutboxEntity NewOutbox()
    {
        return new OutboxEntity
        {
            MessageId = NewId(),
            MessageType = "Test",
            Topic = "topic",
            MessageKey = "key",
            Payload = [1],
            Headers = "{}"
        };
    }

    private async Task SeedInboxAsync(string messageId)
    {
        await using var connection = await db.OpenConnectionAsync();
        await connection.ExecuteAsync("INSERT INTO inbox (consumer_group, message_id) VALUES (@Group, @Id)",
            new { Group, Id = messageId });
    }

    private async Task<int> CountInboxAsync(string messageId)
    {
        await using var connection = await db.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM inbox WHERE message_id = @Id AND consumer_group = @Group",
            new { Id = messageId, Group });
    }
}
