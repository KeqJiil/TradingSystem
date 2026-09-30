IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.outbox')
                 AND name = 'trace_parent')
ALTER TABLE "outbox"
    ADD "trace_parent" VARCHAR(55) NULL;

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.outbox')
                 AND name = 'trace_state')
ALTER TABLE "outbox"
    ADD "trace_state" NVARCHAR(256) NULL;

IF EXISTS (SELECT 1
           FROM sys.types
           WHERE is_table_type = 1
             AND name = 'OutboxEventTvp')
    DROP TYPE dbo.OutboxEventTvp;
GO

IF NOT EXISTS (SELECT 1
               FROM sys.indexes
               WHERE name = 'idx_outbox_pending_created_at'
                 AND object_id = OBJECT_ID('dbo.outbox'))
CREATE INDEX "idx_outbox_pending_created_at" ON "outbox" ("created_at") WHERE "status" != 'COMPLETED';

CREATE TYPE dbo.OutboxEventTvp AS TABLE
(
    "id"             UNIQUEIDENTIFIER NOT NULL,
    "event_type"     NVARCHAR(200)    NOT NULL,
    "payload"        NVARCHAR(MAX)    NOT NULL,
    "aggregate_id"   UNIQUEIDENTIFIER NOT NULL,
    "correlation_id" UNIQUEIDENTIFIER NULL,
    "trace_parent"   VARCHAR(55)      NULL,
    "trace_state"    NVARCHAR(256)    NULL
);
