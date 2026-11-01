using Dapper;
using Messaging.EFCore.Inbox;
using Messaging.EFCore.Outbox;
using Messaging.EFCore.Tests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Messaging.EFCore.Tests.Persistence;

public class ModelConfigurationTests(MssqlFixture db) : IClassFixture<MssqlFixture>
{
    [Fact]
    public async Task InboxTable_Created_ColumnsMatchConfiguration()
    {
        Assert.Equal(
        [
            new Column("consumer_group", "varchar", 200, false),
            new Column("created_at", "datetimeoffset", null, false),
            new Column("message_id", "varchar", 100, false)
        ], await ColumnsAsync("inbox"));
    }

    [Fact]
    public async Task OutboxTable_Created_ColumnsMatchConfiguration()
    {
        Assert.Equal(
        [
            new Column("attempts", "int", null, false),
            new Column("created_at", "datetimeoffset", null, false),
            new Column("headers", "nvarchar", -1, false),
            new Column("id", "bigint", null, false),
            new Column("message_id", "varchar", 100, false),
            new Column("message_key", "varchar", 200, false),
            new Column("message_type", "varchar", 200, false),
            new Column("payload", "varbinary", -1, false),
            new Column("processed_at", "datetimeoffset", null, true),
            new Column("status", "varchar", 20, false),
            new Column("topic", "varchar", 255, false)
        ], await ColumnsAsync("outbox"));
    }

    [Theory]
    [InlineData("inbox")]
    [InlineData("outbox")]
    public async Task CreatedAt_Created_DefaultsToSysDateTimeOffset(string table)
    {
        await using var connection = await db.OpenConnectionAsync();

        var columnDefault = await connection.ExecuteScalarAsync<string>("""
                                                                        SELECT COLUMN_DEFAULT FROM INFORMATION_SCHEMA.COLUMNS
                                                                        WHERE TABLE_NAME = @table AND COLUMN_NAME = 'created_at'
                                                                        """, new { table });

        Assert.Equal("(sysdatetimeoffset())", columnDefault);
    }

    [Fact]
    public async Task InboxTable_Created_PrimaryKeyOnConsumerGroupThenMessageId()
    {
        Assert.Equal(("pk_inbox", "consumer_group,message_id"), await PrimaryKeyAsync("messaging.inbox"));
    }

    [Fact]
    public async Task OutboxTable_Created_PrimaryKeyOnIdWithoutIdentity()
    {
        await using var connection = await db.OpenConnectionAsync();

        var isIdentity = await connection.ExecuteScalarAsync<int>(
            "SELECT COLUMNPROPERTY(OBJECT_ID('messaging.outbox'), 'id', 'IsIdentity')");

        Assert.Equal(("pk_outbox", "id"), await PrimaryKeyAsync("messaging.outbox"));
        Assert.Equal(0, isIdentity);
    }

    [Fact]
    public async Task InboxTable_Created_HasCreatedAtIndex()
    {
        Assert.Equal([new Index("idx_inbox_created_at", "created_at", null, null)], await IndexesAsync("messaging.inbox"));
    }

    [Fact]
    public async Task OutboxTable_Created_HasFilteredPendingAndCompletedIndexes()
    {
        Assert.Equal(
        [
            new Index("idx_outbox_completed", "created_at", null, "([status]='Completed')"),
            new Index("idx_outbox_pending", "id", "processed_at", "([status] IN ('Pending', 'Processing'))")
        ], await IndexesAsync("messaging.outbox"));
    }

    [Fact]
    public async Task OutboxIdSequence_Created_IncrementsByOne()
    {
        await using var connection = await db.OpenConnectionAsync();

        var increment = await connection.ExecuteScalarAsync<long?>(
            "SELECT CAST(increment AS BIGINT) FROM sys.sequences WHERE name = 'outbox_id_seq' AND SCHEMA_NAME(schema_id) = 'messaging'");

        Assert.Equal(1, increment);
    }

    [Fact]
    public async Task SaveChanges_OutboxEntity_StoresStatusAsStringAndFillsCreatedAt()
    {
        var entity = NewOutbox(OutboxStatus.Processing);
        await using (var context = db.CreateContext())
        {
            context.Add(entity);
            await context.SaveChangesAsync();
        }

        await using var connection = await db.OpenConnectionAsync();
        var status = await connection.ExecuteScalarAsync<string>("SELECT status FROM messaging.outbox WHERE id = @Id",
            new { entity.Id });
        await using var readContext = db.CreateContext();
        var stored = await readContext.Set<OutboxEntity>().SingleAsync(x => x.Id == entity.Id);

        Assert.Equal("Processing", status);
        Assert.Equal(OutboxStatus.Processing, stored.Status);
        Assert.NotEqual(default, stored.CreatedAt);
    }

    [Fact]
    public async Task SaveChanges_SeveralOutboxEntities_AssignsIdsInAddOrder()
    {
        var entities = Enumerable.Range(0, 5).Select(_ => NewOutbox(OutboxStatus.Pending)).ToList();
        await using var context = db.CreateContext();

        context.AddRange(entities);
        await context.SaveChangesAsync();

        var ids = entities.Select(x => x.Id).ToList();
        Assert.Equal(ids.Order(), ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task SaveChanges_InboxEntity_FillsCreatedAt()
    {
        var entity = NewInbox();
        await using (var context = db.CreateContext())
        {
            context.Add(entity);
            await context.SaveChangesAsync();
        }

        await using var readContext = db.CreateContext();
        var stored = await readContext.Set<InboxEntity>()
            .SingleAsync(x => x.ConsumerGroup == entity.ConsumerGroup && x.MessageId == entity.MessageId);

        Assert.NotEqual(default, stored.CreatedAt);
    }

    [Fact]
    public async Task SaveChanges_DuplicateInboxEntity_ThrowsUniqueViolation()
    {
        var entity = NewInbox();
        await using (var context = db.CreateContext())
        {
            context.Add(entity);
            await context.SaveChangesAsync();
        }

        await using var duplicateContext = db.CreateContext();
        duplicateContext.Add(new InboxEntity { ConsumerGroup = entity.ConsumerGroup, MessageId = entity.MessageId });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());

        Assert.Equal(2627, Assert.IsType<SqlException>(ex.InnerException).Number);
    }

    private static OutboxEntity NewOutbox(OutboxStatus status)
    {
        return new OutboxEntity
        {
            MessageId = Guid.NewGuid().ToString(),
            MessageType = "Test",
            Topic = "topic",
            MessageKey = "key",
            Payload = [1, 2, 3],
            Headers = "{}",
            Status = status
        };
    }

    private static InboxEntity NewInbox()
    {
        return new InboxEntity
            { ConsumerGroup = "group", MessageId = Guid.NewGuid().ToString() };
    }

    private async Task<List<Column>> ColumnsAsync(string table)
    {
        await using var connection = await db.OpenConnectionAsync();
        return (await connection.QueryAsync<Column>("""
                                                    SELECT COLUMN_NAME AS Name, DATA_TYPE AS Type, CHARACTER_MAXIMUM_LENGTH AS Length,
                                                           CAST(CASE IS_NULLABLE WHEN 'YES' THEN 1 ELSE 0 END AS BIT) AS Nullable
                                                    FROM INFORMATION_SCHEMA.COLUMNS
                                                    WHERE TABLE_NAME = @table
                                                    ORDER BY COLUMN_NAME
                                                    """, new { table })).ToList();
    }

    private async Task<(string Name, string Columns)> PrimaryKeyAsync(string table)
    {
        await using var connection = await db.OpenConnectionAsync();
        return await connection.QuerySingleAsync<(string, string)>("""
                                                                   SELECT kc.name, STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal)
                                                                   FROM sys.key_constraints kc
                                                                            JOIN sys.index_columns ic
                                                                                 ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
                                                                            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                                                                   WHERE kc.type = 'PK' AND kc.parent_object_id = OBJECT_ID(@table)
                                                                   GROUP BY kc.name
                                                                   """, new { table });
    }

    private async Task<List<Index>> IndexesAsync(string table)
    {
        await using var connection = await db.OpenConnectionAsync();
        return (await connection.QueryAsync<Index>("""
                                                   SELECT i.name AS Name,
                                                          STRING_AGG(CASE WHEN ic.is_included_column = 0 THEN c.name END, ',')
                                                                     WITHIN GROUP (ORDER BY ic.key_ordinal) AS KeyColumns,
                                                          STRING_AGG(CASE WHEN ic.is_included_column = 1 THEN c.name END, ',') AS IncludedColumns,
                                                          i.filter_definition AS Filter
                                                   FROM sys.indexes i
                                                            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                                                            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                                                   WHERE i.object_id = OBJECT_ID(@table) AND i.is_primary_key = 0
                                                   GROUP BY i.name, i.filter_definition
                                                   ORDER BY i.name
                                                   """, new { table })).ToList();
    }

    private record Column(string Name, string Type, int? Length, bool Nullable);

    private record Index(string Name, string KeyColumns, string? IncludedColumns, string? Filter);
}