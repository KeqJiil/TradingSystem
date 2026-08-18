CREATE TABLE "events_store" (
    "event_id" UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    "aggregate_id" UNIQUEIDENTIFIER NOT NULL,
    "version" BIGINT NOT NULL,
    "event_type" NVARCHAR(50) NOT NULL,
    "price_change" DECIMAL(20, 4) NOT NULL,
    "created_at" DATETIMEOFFSET NOT NULL DEFAULT(GETDATE())
);

CREATE TABLE "stock_data" (
    "id" UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    "name" NVARCHAR(20) NOT NULL,
    "is_open_to_trade" BIT NOT NULL,
    "trading_start_time" TIME,
    "trading_end_time" TIME,
    "currency" NVARCHAR(3),
    "created_at" DATETIMEOFFSET NOT NULL DEFAULT(GETDATE())
);

CREATE TABLE "stock_data_projection" (
    "aggregate_id" UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    "name" NVARCHAR(20) NOT NULL,
    "is_open_to_trade" BIT NOT NULL,
    "price" DECIMAL(20, 4) NOT NULL,
    "version" BIGINT NOT NULL,
    "trading_start_time" TIME,
    "trading_end_time" TIME,
    "currency" NVARCHAR(3),
    "updated_at" DATETIMEOFFSET NOT NULL DEFAULT(GETDATE()),
    "created_at" DATETIMEOFFSET NOT NULL DEFAULT(GETDATE())
);

CREATE INDEX "idx_event_store_aggregate_id" ON "events_store" ("aggregate_id");
CREATE UNIQUE INDEX "idx_event_store_version" ON "events_store" ("aggregate_id", "version");
CREATE INDEX "idx_stock_projection_time" ON "stock_data_projection" ("aggregate_id", "created_at");