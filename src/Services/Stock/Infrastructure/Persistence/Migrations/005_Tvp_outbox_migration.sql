CREATE TYPE dbo.OutboxEventTvp AS TABLE
(
    "id"           UNIQUEIDENTIFIER NOT NULL,
    "event_type"   NVARCHAR(200)    NOT NULL,
    "payload"      NVARCHAR(MAX)    NOT NULL,
    "aggregate_id" UNIQUEIDENTIFIER NOT NULL
);