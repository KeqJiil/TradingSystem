using Dapper;
using Messaging.Dapper.Migrations;
using Messaging.Dapper.Tests.Integration.Infrastructure;

namespace Messaging.Dapper.Tests.Integration.Migrations;

public class DbMigratorTests(MssqlFixture db) : IClassFixture<MssqlFixture>
{
    [Fact]
    public async Task ApplyMigrations_EmptyDatabase_CreatesInboxAndOutbox()
    {
        var connectionString = db.NewDatabaseConnectionString();

        DbMigrator.ApplyMigrations(connectionString, typeof(DbMigratorTests).Assembly, MssqlFixture.LibraryScripts);

        await using var connection = await db.OpenConnectionAsync(connectionString);
        Assert.NotNull(await connection.ExecuteScalarAsync<int?>("SELECT OBJECT_ID('messaging.inbox', 'U')"));
        Assert.NotNull(await connection.ExecuteScalarAsync<int?>("SELECT OBJECT_ID('messaging.outbox', 'U')"));
        Assert.NotNull(await connection.ExecuteScalarAsync<int?>("SELECT TYPE_ID('messaging.outbox_event_tvp')"));
    }

    [Fact]
    public void ApplyMigrations_InvalidScript_Throws()
    {
        var connectionString = db.NewDatabaseConnectionString();

        Assert.Throws<InvalidOperationException>(() => DbMigrator.ApplyMigrations(connectionString,
            typeof(DbMigratorTests).Assembly, [new MigrationScript("broken.sql", "THIS IS NOT SQL")]));
    }

    [Fact]
    public async Task ApplyMigrations_Twice_DoesNotReapplyScripts()
    {
        var connectionString = db.NewDatabaseConnectionString();

        DbMigrator.ApplyMigrations(connectionString, typeof(DbMigratorTests).Assembly, MssqlFixture.LibraryScripts);
        DbMigrator.ApplyMigrations(connectionString, typeof(DbMigratorTests).Assembly, MssqlFixture.LibraryScripts);

        await using var connection = await db.OpenConnectionAsync(connectionString);
        Assert.Equal(2, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SchemaVersions"));
    }
}
