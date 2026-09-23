IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.stock_data_projection')
                 AND name = 'name_version')
ALTER TABLE "stock_data_projection"
    ADD "name_version" BIGINT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.stock_data_projection')
                 AND name = 'trading_time_version')
ALTER TABLE "stock_data_projection"
    ADD "trading_time_version" BIGINT NOT NULL DEFAULT 0;
