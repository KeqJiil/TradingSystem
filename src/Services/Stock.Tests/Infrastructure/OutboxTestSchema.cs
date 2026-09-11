using Dapper;

namespace Stock.Tests.Infrastructure;

public static class OutboxTestSchema
{
    public static async Task EnsureCreatedAsync(TestDbContext dbContext)
    {
        await dbContext.Connection.ExecuteAsync("""
            IF OBJECT_ID('outbox') IS NULL
            CREATE TABLE outbox(
                id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                aggregate_id UNIQUEIDENTIFIER NOT NULL,
                payload NVARCHAR(MAX) NOT NULL,
                event_type NVARCHAR(50) NOT NULL,
                status VARCHAR(20) NOT NULL DEFAULT 'PENDING',
                processed_at DATETIMEOFFSET NULL,
                created_at DATETIMEOFFSET NOT NULL DEFAULT(GETDATE()),
                attempts INT NOT NULL DEFAULT(0)
            );
            """);

        await dbContext.Connection.ExecuteAsync("""
            IF TYPE_ID('dbo.OutboxEventTvp') IS NULL
            EXEC('CREATE TYPE dbo.OutboxEventTvp AS TABLE
            (
                "id"           UNIQUEIDENTIFIER NOT NULL,
                "event_type"   NVARCHAR(200)    NOT NULL,
                "payload"      NVARCHAR(MAX)    NOT NULL,
                "aggregate_id" UNIQUEIDENTIFIER NOT NULL
            )');
            """);
    }
}
