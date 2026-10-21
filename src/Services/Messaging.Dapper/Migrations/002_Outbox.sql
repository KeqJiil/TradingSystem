IF SCHEMA_ID('messaging') IS NULL
    EXEC ('CREATE SCHEMA messaging');
GO

IF OBJECT_ID('messaging.outbox', 'U') IS NULL
    CREATE TABLE "messaging"."outbox"
    (
        "id"           BIGINT IDENTITY (1, 1) NOT NULL CONSTRAINT "pk_outbox" PRIMARY KEY CLUSTERED,
        "message_id"   VARCHAR(100)     NOT NULL,
        "message_type" VARCHAR(200)     NOT NULL,
        "topic"        VARCHAR(255)     NOT NULL,
        "message_key"  VARCHAR(200)     NOT NULL,
        "payload"      VARBINARY(MAX)   NOT NULL,
        "headers"      NVARCHAR(MAX)    NOT NULL,
        "status"       VARCHAR(20)      NOT NULL CONSTRAINT "df_outbox_status" DEFAULT ('PENDING'),
        "attempts"     INT              NOT NULL CONSTRAINT "df_outbox_attempts" DEFAULT (0),
        "processed_at" DATETIMEOFFSET   NULL,
        "created_at"   DATETIMEOFFSET   NOT NULL CONSTRAINT "df_outbox_created_at" DEFAULT (SYSDATETIMEOFFSET())
    );
GO

IF NOT EXISTS (SELECT 1
               FROM sys.indexes
               WHERE object_id = OBJECT_ID('messaging.outbox')
                 AND name = 'idx_outbox_pending')
    CREATE INDEX "idx_outbox_pending" ON "messaging"."outbox" ("id") INCLUDE ("processed_at")
        WHERE "status" IN ('PENDING', 'PROCESSING');
GO

IF NOT EXISTS (SELECT 1
               FROM sys.indexes
               WHERE object_id = OBJECT_ID('messaging.outbox')
                 AND name = 'idx_outbox_completed')
    CREATE INDEX "idx_outbox_completed" ON "messaging"."outbox" ("created_at") WHERE "status" = 'COMPLETED';
GO

IF TYPE_ID('messaging.outbox_event_tvp') IS NULL
    CREATE TYPE messaging.outbox_event_tvp AS TABLE
    (
        "ordinal"      INT              NOT NULL,
        "message_id"   VARCHAR(100)     NOT NULL,
        "message_type" VARCHAR(200)     NOT NULL,
        "topic"        VARCHAR(255)     NOT NULL,
        "message_key"  VARCHAR(200)     NOT NULL,
        "payload"      VARBINARY(MAX)   NOT NULL,
        "headers"      NVARCHAR(MAX)    NOT NULL,
        "attempts"     INT              NOT NULL
    );
