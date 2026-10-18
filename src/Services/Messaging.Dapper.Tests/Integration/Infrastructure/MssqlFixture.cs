using Messaging.Dapper.Migrations;
using Messaging.Testing;

namespace Messaging.Dapper.Tests.Integration.Infrastructure;

public sealed class MssqlFixture : MssqlFixtureBase
{
    public static MigrationScript[] LibraryScripts =>
        [EmbeddedMigration.Read("001_Inbox.sql"), EmbeddedMigration.Read("002_Outbox.sql")];

    public override Task CreateSchemaAsync(string connectionString) =>
        Task.Run(() => DbMigrator.ApplyMigrations(connectionString, typeof(MssqlFixture).Assembly, LibraryScripts));
}
