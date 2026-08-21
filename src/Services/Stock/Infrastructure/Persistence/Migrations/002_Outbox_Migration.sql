CREATE TABLE "outbox"(
    "id" UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    "aggregate_id" UNIQUEIDENTIFIER NOT NULL,
    "payload" NVARCHAR(MAX) NOT NULL,
    "event_type" NVARCHAR(50) NOT NULL,
    "status" VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    "processed_at" DATETIMEOFFSET NULL,
    "created_at" DATETIMEOFFSET NOT NULL DEFAULT(GETDATE())
);

CREATE INDEX "idx_outbox_aggregate_id" ON "outbox" ("aggregate_id");
CREATE INDEX "idx_outbox_status" ON "outbox" ("status") WHERE "status" != 'COMPLETED';