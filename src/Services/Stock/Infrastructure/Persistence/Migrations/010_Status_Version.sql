IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.stock_data')
                 AND name = 'status_version')
ALTER TABLE "stock_data"
    ADD "status_version" BIGINT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1
               FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.stock_data_projection')
                 AND name = 'status_version')
ALTER TABLE "stock_data_projection"
    ADD "status_version" BIGINT NOT NULL DEFAULT 0;
