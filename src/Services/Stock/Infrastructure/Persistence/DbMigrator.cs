using Messaging.Dapper.Migrations;

namespace Stock.Infrastructure.Persistence;

public static class DbMigrator
{
    public static void ApplyMigrations(string connectionString)
    {
        global::Messaging.Dapper.Migrations.DbMigrator.ApplyMigrations(connectionString,
            typeof(DbMigrator).Assembly, MessagingMigrations.All);
    }
}
