IF OBJECT_ID('dbo.inbox', 'U') IS NULL
    CREATE TABLE "inbox"
    (
        "consumer_group" VARCHAR(200)      NOT NULL,
        "message_id"     VARCHAR(100)      NOT NULL,
        "created_at"     DATETIMEOFFSET    NOT NULL CONSTRAINT "df_inbox_created_at" DEFAULT (SYSDATETIMEOFFSET()),
        CONSTRAINT "pk_inbox" PRIMARY KEY CLUSTERED ("consumer_group", "message_id") WITH (IGNORE_DUP_KEY = ON)
    );
GO

IF NOT EXISTS (SELECT 1
               FROM sys.indexes
               WHERE object_id = OBJECT_ID('dbo.inbox')
                 AND name = 'idx_inbox_created_at')
    CREATE INDEX "idx_inbox_created_at" ON "inbox" ("created_at");
