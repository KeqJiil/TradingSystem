CREATE TABLE "hourly_stock_data_projection"
(
    "id"               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    "aggregate_id"     UNIQUEIDENTIFIER NOT NULL,
    "open_price"       DECIMAL(20, 4)   NOT NULL,
    "low_price"        DECIMAL(20, 4)   NOT NULL,
    "high_price"       DECIMAL(20, 4)   NOT NULL,
    "close_price"      DECIMAL(20, 4)   NOT NULL,
    "price_difference" DECIMAL(20, 4)   NOT NULL,
    "last_version"     BIGINT           NOT NULL,
    "date"             DATE             NOT NULL DEFAULT (GETDATE())
);

CREATE UNIQUE INDEX "idx_hourly_stock_projection_aggregate_id_date_hour" ON "hourly_stock_data_projection" ("aggregate_id", "date");