IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.outbox')
                 AND name = 'correlation_id')
ALTER TABLE "outbox"
    ADD "correlation_id" UNIQUEIDENTIFIER NULL;

IF EXISTS (SELECT 1
           FROM sys.types
           WHERE is_table_type = 1
             AND name = 'OutboxEventTvp')
    DROP TYPE dbo.OutboxEventTvp;
GO

IF NOT EXISTS (SELECT 1
               FROM sys.indexes
               WHERE name = 'idx_outbox_correlation_id'
                 AND object_id = OBJECT_ID('dbo.outbox'))
CREATE INDEX "idx_outbox_correlation_id" ON "outbox" ("correlation_id") WHERE "correlation_id" IS NOT NULL;

CREATE TYPE dbo.OutboxEventTvp AS TABLE
(
    "id"             UNIQUEIDENTIFIER NOT NULL,
    "event_type"     NVARCHAR(200)    NOT NULL,
    "payload"        NVARCHAR(MAX)    NOT NULL,
    "aggregate_id"   UNIQUEIDENTIFIER NOT NULL,
    "correlation_id" UNIQUEIDENTIFIER NULL
);