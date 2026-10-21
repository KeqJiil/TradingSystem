namespace Messaging.Dapper.Migrations;

public static class MessagingMigrations
{
    public static MigrationScript Inbox => EmbeddedMigration.Read("001_Inbox.sql");
    public static MigrationScript Outbox => EmbeddedMigration.Read("002_Outbox.sql");
    public static MigrationScript[] All => [Inbox, Outbox];
}
